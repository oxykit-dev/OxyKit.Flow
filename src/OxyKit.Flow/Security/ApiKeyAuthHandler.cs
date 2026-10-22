using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OxyKit.Flow.Common;
using FlowSecurityOptions = OxyKit.Flow.Services.SecurityOptions;

namespace OxyKit.Flow.Security;

/// <summary>
/// Shared-secret API key authentication (header from <see cref="FlowSecurityOptions"/>).
/// </summary>
public sealed class ApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<FlowSecurityOptions> securityOptions
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.GetEndpoint()?.Metadata.OfType<AllowAnonymousAttribute>().Any() == true)
            return Task.FromResult(AuthenticateResult.NoResult());

        var settings = securityOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return Task.FromResult(AuthenticateResult.Fail("Flow API key is not configured."));

        if (
            !Request.Headers.TryGetValue(settings.ApiKeyHeader, out var provided)
            || !string.Equals(provided.ToString(), settings.ApiKey, StringComparison.Ordinal)
        )
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthorized."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "flow-api-key")],
            AuthSchemeNames.ApiKey
        );
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthSchemeNames.ApiKey);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

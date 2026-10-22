using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using OxyKit.Flow.Security;
using FlowSecurityOptions = OxyKit.Flow.Services.SecurityOptions;

namespace OxyKit.Flow.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOxyKitFlowHost(this IServiceCollection services)
    {
        services
            .AddOptions<FlowSecurityOptions>()
            .BindConfiguration(FlowSecurityOptions.SectionName)
            .Validate(
                o =>
                    !string.IsNullOrWhiteSpace(o.ApiKey)
                    && !string.Equals(o.ApiKey, "change-me-in-production", StringComparison.Ordinal),
                "Security:ApiKey is required and must not be the placeholder value."
            )
            .ValidateOnStart();

        services.AddFastEndpoints();
        services
            .AddAuthentication(AuthSchemeNames.ApiKey)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(
                AuthSchemeNames.ApiKey,
                _ => { }
            );
        services.AddAuthorization();
        return services;
    }
}

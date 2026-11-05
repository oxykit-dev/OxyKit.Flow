using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace OxyKit.Flow.Tests;

public sealed class DualAuthTests : IClassFixture<FlowWebApplicationFactory>, IDisposable
{
    private const string JwtKey = "dev-local-jwt-signing-key-at-least-32-chars!";
    private const string JwtIssuer = "OxyKit.Mail";
    private const string JwtAudience = "OxyKit.Mail";

    private readonly FlowWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DualAuthTests(FlowWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task ListJobs_SucceedsWithBearerAdmin()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Admin")
        );

        var response = await _client.GetAsync("/api/v1/jobs/", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListJobs_SucceedsWithBearerUser()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("User")
        );

        var response = await _client.GetAsync("/api/v1/jobs/", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListJobs_ForbidsSender()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Sender")
        );

        var response = await _client.GetAsync("/api/v1/jobs/", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateJob_ForbidsUser_AllowsAdmin()
    {
        var body = new
        {
            name = "jwt-create",
            render = new
            {
                input = "table",
                output = "html",
                body = new { sheets = Array.Empty<object>() },
            },
            outputs = new[] { new { provider = "file", destination = "output/jwt.html" } },
        };

        using var userClient = _factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("User")
        );
        var userResponse = await userClient.PostAsJsonAsync(
            "/api/v1/jobs/",
            body,
            TestContext.Current.CancellationToken
        );
        userResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("Admin")
        );
        var adminResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/jobs/",
            body,
            TestContext.Current.CancellationToken
        );
        // Auth cleared; body may still fail render/output validation depending on engine reachability.
        adminResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        adminResponse.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    private static string CreateToken(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "flow-test-user"),
            new(ClaimTypes.Name, "tester"),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OxyKit.Flow.Infrastructure;
using OxyKit.Flow.Infrastructure.Postgres;
using OxyKit.Flow.Infrastructure.Sqlite;
using OxyKit.Flow.Security;
using FlowSecurityOptions = OxyKit.Flow.Services.SecurityOptions;

namespace OxyKit.Flow.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOxyKitFlowHost(
        this IServiceCollection services,
        IConfiguration configuration
    )
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
            .AddAuthentication(options =>
            {
                options.DefaultScheme = AuthSchemeNames.ApiKeyOrBearer;
                options.DefaultChallengeScheme = AuthSchemeNames.ApiKeyOrBearer;
            })
            .AddPolicyScheme(
                AuthSchemeNames.ApiKeyOrBearer,
                "API key or Bearer JWT",
                options =>
                {
                    options.ForwardDefaultSelector = context =>
                    {
                        var header =
                            configuration["Security:ApiKeyHeader"] ?? "X-OxyKit-Flow-Key";
                        if (context.Request.Headers.ContainsKey(header))
                            return AuthSchemeNames.ApiKey;
                        return AuthSchemeNames.Bearer;
                    };
                }
            )
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(
                AuthSchemeNames.ApiKey,
                _ => { }
            )
            .AddJwtBearer(
                AuthSchemeNames.Bearer,
                options =>
                {
                    var jwtKey = configuration["Security:Jwt:Key"];
                    if (string.IsNullOrWhiteSpace(jwtKey))
                    {
                        // Keep scheme registered; tokens fail validation until Jwt is configured.
                        jwtKey = "unconfigured-jwt-key-placeholder-32chars!";
                    }

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = configuration["Security:Jwt:Issuer"],
                        ValidAudience = configuration["Security:Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtKey)
                        ),
                        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    };
                }
            );

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthPolicies.JobsWrite,
                policy =>
                {
                    policy.AddAuthenticationSchemes(AuthSchemeNames.ApiKeyOrBearer);
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("Admin");
                }
            );
            options.AddPolicy(
                AuthPolicies.JobsRead,
                policy =>
                {
                    policy.AddAuthenticationSchemes(AuthSchemeNames.ApiKeyOrBearer);
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("Admin", "User");
                }
            );
        });
        return services;
    }

    /// <summary>
    /// Registers EF + claim store for <c>SQLite</c> or <c>PostgreSQL</c> (Mail-aligned provider switch).
    /// </summary>
    public static IServiceCollection AddOxyKitFlowDatabase(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var dbProvider = configuration["DatabaseProvider"] ?? "SQLite";

        var migrationsAssembly = dbProvider switch
        {
            "PostgreSQL" => "OxyKit.Flow.Infrastructure.Postgres",
            "SQLite" => "OxyKit.Flow.Infrastructure.Sqlite",
            _ => throw new InvalidOperationException(
                $"Unsupported DatabaseProvider '{dbProvider}'. Use SQLite or PostgreSQL."
            ),
        };

        if (dbProvider == "PostgreSQL")
            services.AddPostgresDataAccess(configuration, migrationsAssembly);
        else
            services.AddSqliteDataAccess(configuration, migrationsAssembly);

        return services;
    }

    public static void ApplyOxyKitFlowMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDbContext>();
        db.Database.Migrate();
    }
}

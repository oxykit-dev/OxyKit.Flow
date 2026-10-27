using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using OxyKit.Flow.Infrastructure;
using OxyKit.Flow.Infrastructure.Postgres;
using OxyKit.Flow.Infrastructure.Sqlite;
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

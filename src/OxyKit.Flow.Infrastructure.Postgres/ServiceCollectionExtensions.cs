using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OxyKit.Flow.Infrastructure;

namespace OxyKit.Flow.Infrastructure.Postgres;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresDataAccess(
        this IServiceCollection services,
        IConfiguration configuration,
        string? migrationsAssembly = null
    )
    {
        var connectionString = configuration.GetConnectionString("PostgreSQLConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:PostgreSQLConnection is required when DatabaseProvider=PostgreSQL."
            );

        services.AddDbContext<FlowDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                    npgsqlOptions.MigrationsAssembly(
                        migrationsAssembly ?? "OxyKit.Flow.Infrastructure.Postgres"
                    )
            )
        );
        services.AddScoped<IJobClaimStore, PostgresJobClaimStore>();
        return services;
    }
}

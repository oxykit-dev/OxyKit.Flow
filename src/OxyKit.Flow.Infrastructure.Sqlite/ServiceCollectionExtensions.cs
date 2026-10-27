using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OxyKit.Flow.Infrastructure;

namespace OxyKit.Flow.Infrastructure.Sqlite;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteDataAccess(
        this IServiceCollection services,
        IConfiguration configuration,
        string? migrationsAssembly = null
    )
    {
        var connectionString = configuration.GetConnectionString("SQLiteConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:SQLiteConnection is required when DatabaseProvider=SQLite."
            );

        EnsureSqliteDirectory(connectionString);

        services.AddDbContext<FlowDbContext>(options =>
            options.UseSqlite(
                connectionString,
                sqliteOptions =>
                    sqliteOptions.MigrationsAssembly(
                        migrationsAssembly ?? "OxyKit.Flow.Infrastructure.Sqlite"
                    )
            )
        );
        services.AddScoped<IJobClaimStore, SqliteJobClaimStore>();
        return services;
    }

    private static void EnsureSqliteDirectory(string connectionString)
    {
        const string dataSourcePrefix = "Data Source=";
        var startIndex = connectionString.IndexOf(
            dataSourcePrefix,
            StringComparison.OrdinalIgnoreCase
        );
        if (startIndex < 0)
            return;

        var filePath = connectionString[(startIndex + dataSourcePrefix.Length)..].Trim();
        // Strip optional Mode= / Cache= suffixes after semicolon
        var semi = filePath.IndexOf(';');
        if (semi >= 0)
            filePath = filePath[..semi].Trim();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}

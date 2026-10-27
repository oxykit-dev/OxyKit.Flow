using Microsoft.EntityFrameworkCore;

namespace OxyKit.Flow.Infrastructure;

/// <summary>
/// Provider-agnostic helpers for EF update failures.
/// </summary>
public static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// True when the failure is a unique / duplicate-key constraint (SQLite, PostgreSQL).
    /// </summary>
    public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            var typeName = e.GetType().Name;

            if (typeName is "PostgresException")
            {
                var sqlState = e.GetType().GetProperty("SqlState")?.GetValue(e) as string;
                if (sqlState == "23505")
                    return true;
            }

            if (typeName is "SqliteException")
            {
                // SQLITE_CONSTRAINT = 19; SQLITE_CONSTRAINT_UNIQUE = 2067
                var code = e.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(e);
                if (code is 19 or 2067)
                    return true;
                code = e.GetType().GetProperty("SqliteErrorCode")?.GetValue(e);
                if (code is 19)
                    return true;
            }

            if (
                e.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            )
                return true;
        }

        return false;
    }
}

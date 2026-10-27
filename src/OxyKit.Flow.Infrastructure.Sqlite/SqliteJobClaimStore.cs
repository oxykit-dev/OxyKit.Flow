using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OxyKit.Flow.Infrastructure;

namespace OxyKit.Flow.Infrastructure.Sqlite;

/// <summary>
/// Claims queued jobs inside a SQLite write transaction (single-writer safe).
/// </summary>
public sealed class SqliteJobClaimStore(FlowDbContext db, ILogger<SqliteJobClaimStore> logger)
    : IJobClaimStore
{
    public Task<int> ReclaimStuckAsync(
        TimeSpan processingTimeout,
        CancellationToken cancellationToken = default
    ) => JobStuckReclaim.ReclaimAsync(db, processingTimeout, logger, cancellationToken);

    public async Task<IReadOnlyList<Job>> ClaimQueuedAsync(
        int batchSize,
        CancellationToken cancellationToken = default
    )
    {
        var take = Math.Max(1, batchSize);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var queued = await db
            .Jobs.Where(j => j.Status == JobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        foreach (var job in queued)
            job.MarkAsProcessing();

        if (queued.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return queued;
    }
}

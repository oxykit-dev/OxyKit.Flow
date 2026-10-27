using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OxyKit.Flow.Infrastructure;

namespace OxyKit.Flow.Infrastructure.Postgres;

/// <summary>
/// Claims queued jobs with <c>FOR UPDATE SKIP LOCKED</c> for multi-worker safety.
/// </summary>
public sealed class PostgresJobClaimStore(FlowDbContext db, ILogger<PostgresJobClaimStore> logger)
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

        var ids = await db
            .Database.SqlQuery<Guid>(
                $"""
                SELECT "Id" AS "Value"
                FROM "Jobs"
                WHERE "Status" = 'Queued'
                ORDER BY "CreatedAt"
                FOR UPDATE SKIP LOCKED
                LIMIT {take}
                """
            )
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            await tx.CommitAsync(cancellationToken);
            return [];
        }

        var queued = await db.Jobs.Where(j => ids.Contains(j.Id)).ToListAsync(cancellationToken);
        // Preserve CreatedAt order from the SKIP LOCKED select
        var byId = queued.ToDictionary(j => j.Id);
        var ordered = new List<Job>(ids.Count);
        foreach (var id in ids)
        {
            if (byId.TryGetValue(id, out var job))
                ordered.Add(job);
        }

        foreach (var job in ordered)
            job.MarkAsProcessing();

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return ordered;
    }
}

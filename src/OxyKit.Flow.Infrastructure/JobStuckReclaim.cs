using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OxyKit.Flow.Infrastructure;

/// <summary>
/// Shared stuck-job reclaim used by SQLite and PostgreSQL claim stores.
/// </summary>
public static class JobStuckReclaim
{
    public static async Task<int> ReclaimAsync(
        FlowDbContext db,
        TimeSpan processingTimeout,
        ILogger logger,
        CancellationToken cancellationToken = default
    )
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(30, processingTimeout.TotalSeconds));
        var cutoff = DateTime.UtcNow - timeout;

        var stuck = await db
            .Jobs.Where(j =>
                j.Status == JobStatus.Processing
                && j.ProcessingStartedAt != null
                && j.ProcessingStartedAt <= cutoff
            )
            .ToListAsync(cancellationToken);

        foreach (var job in stuck)
        {
            if (job.CanBeRetried())
            {
                job.MarkAsQueuedForRetry(
                    $"Processing timed out after {timeout.TotalSeconds:0}s; re-queued for retry."
                );
                logger.LogWarning(
                    "Job {JobId} stuck; re-queued (attempt {Attempt})",
                    job.Id,
                    job.AttemptCount
                );
            }
            else
            {
                job.MarkAsDeadLettered(
                    $"Max attempts ({job.MaxAttempts}) exceeded after processing timeout."
                );
                logger.LogError("Job {JobId} dead-lettered after stuck timeout", job.Id);
            }
        }

        if (stuck.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return stuck.Count;
    }
}

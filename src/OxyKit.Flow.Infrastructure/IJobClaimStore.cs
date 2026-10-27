namespace OxyKit.Flow.Infrastructure;

/// <summary>
/// Provider-specific job claim / stuck reclaim for the background queue.
/// </summary>
public interface IJobClaimStore
{
    /// <summary>
    /// Re-queue or dead-letter jobs stuck in <see cref="JobStatus.Processing"/> past the timeout.
    /// </summary>
    Task<int> ReclaimStuckAsync(TimeSpan processingTimeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claim up to <paramref name="batchSize"/> queued jobs (marks them Processing).
    /// </summary>
    Task<IReadOnlyList<Job>> ClaimQueuedAsync(int batchSize, CancellationToken cancellationToken = default);
}

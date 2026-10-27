namespace OxyKit.Flow.Infrastructure;

/// <summary>
/// Lifecycle of a persisted orchestration job.
/// </summary>
public enum JobStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    DeadLettered,
}

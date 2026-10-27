namespace OxyKit.Flow.Infrastructure;

/// <summary>
/// Per-output delivery checkpoint for a job.
/// </summary>
public enum JobDeliveryStatus
{
    Pending,
    Succeeded,
    Failed,
}

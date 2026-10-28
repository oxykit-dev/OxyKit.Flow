namespace OxyKit.Flow.Infrastructure;

/// <summary>
/// One delivery target for a job, checkpointed independently so retries skip succeeded outputs.
/// </summary>
public sealed class JobDelivery
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public Job? Job { get; set; }

    /// <summary>Stable index into the job's outputs list.</summary>
    public int Ordinal { get; set; }

    public required string Provider { get; set; }

    public required string Destination { get; set; }

    public JobDeliveryStatus Status { get; set; } = JobDeliveryStatus.Pending;

    /// <summary>Provider-specific id (e.g. Mail <c>emailId</c>).</summary>
    public string? ExternalId { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public void MarkAsSucceeded(string? externalId = null)
    {
        Status = JobDeliveryStatus.Succeeded;
        ExternalId = externalId ?? ExternalId;
        LastError = null;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string reason)
    {
        Status = JobDeliveryStatus.Failed;
        LastError = reason.Length <= 4000 ? reason : reason[..4000];
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Reset a non-succeeded delivery so a requeued job can try again.</summary>
    public void MarkAsPendingForRetry()
    {
        Status = JobDeliveryStatus.Pending;
        LastError = null;
        ExternalId = null;
        CompletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

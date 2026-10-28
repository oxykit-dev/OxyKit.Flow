namespace OxyKit.Flow.Infrastructure;

/// <summary>
/// Persisted render + deliver job with retry and client idempotency tracking.
/// </summary>
public sealed class Job
{
    public Guid Id { get; set; }

    /// <summary>Optional human label (e.g. "weekly-orders").</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Client-supplied key; unique when set. Same key returns the existing job instead of creating another.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Queued;

    /// <summary>
    /// Registered <c>IDocumentRenderer.Name</c> (default <c>oxykit-render</c>).
    /// </summary>
    public required string RenderEngine { get; set; }

    /// <summary>Render input format, e.g. <c>table</c>, <c>template</c>.</summary>
    public required string RenderInput { get; set; }

    /// <summary>Render output format, e.g. <c>pdf</c>, <c>html</c>.</summary>
    public required string RenderOutput { get; set; }

    /// <summary>JSON body posted to the render engine.</summary>
    public required string RenderBodyJson { get; set; }

    /// <summary>JSON array of delivery targets (<c>provider</c>, <c>destination</c>, optional mail fields).</summary>
    public required string OutputsJson { get; set; }

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 3;

    public DateTime? ProcessingStartedAt { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public void MarkAsProcessing()
    {
        Status = JobStatus.Processing;
        ProcessingStartedAt = DateTime.UtcNow;
        LastAttemptAt = DateTime.UtcNow;
        AttemptCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted()
    {
        Status = JobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string reason)
    {
        Status = JobStatus.Failed;
        LastError = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsDeadLettered(string reason)
    {
        Status = JobStatus.DeadLettered;
        LastError = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Re-queue after a retryable failure.</summary>
    public void MarkAsQueuedForRetry(string reason)
    {
        Status = JobStatus.Queued;
        LastError = reason;
        ProcessingStartedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Operator requeue (e.g. dead-letter retry): fresh attempt budget, back to Queued.</summary>
    public void MarkAsRequeued()
    {
        Status = JobStatus.Queued;
        AttemptCount = 0;
        ProcessingStartedAt = null;
        CompletedAt = null;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanBeRetried() => AttemptCount < MaxAttempts;

    public bool IsStuck(TimeSpan timeout) =>
        Status == JobStatus.Processing
        && ProcessingStartedAt.HasValue
        && DateTime.UtcNow - ProcessingStartedAt.Value > timeout;
}

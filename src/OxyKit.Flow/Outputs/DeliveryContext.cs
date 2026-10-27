namespace OxyKit.Flow.Outputs;

/// <summary>
/// Job-scoped context for one output attempt (idempotency + ordinal).
/// </summary>
public sealed class DeliveryContext
{
    public required Guid JobId { get; init; }

    public required int Ordinal { get; init; }

    /// <summary>Stable key for sinks that support client idempotency (e.g. Mail).</summary>
    public string IdempotencyKey => $"{JobId:N}:{Ordinal}";
}

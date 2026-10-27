namespace OxyKit.Flow.Outputs;

/// <summary>
/// Result of a single output send (optional external id for checkpoints).
/// </summary>
public sealed class DeliveryResult
{
    public static DeliveryResult Empty { get; } = new();

    public string? ExternalId { get; init; }

    public static DeliveryResult WithExternalId(string externalId) =>
        new() { ExternalId = externalId };
}

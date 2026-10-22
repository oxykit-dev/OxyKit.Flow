namespace OxyKit.Flow.Abstractions;

/// <summary>
/// Bytes produced by a render engine, ready for delivery.
/// </summary>
public sealed class Artifact
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public string? FileName { get; init; }
}

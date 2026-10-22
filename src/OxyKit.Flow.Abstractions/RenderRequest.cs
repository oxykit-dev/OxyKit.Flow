using System.Text.Json;

namespace OxyKit.Flow.Abstractions;

/// <summary>
/// Render step passed to <see cref="IDocumentRenderer"/>.
/// OxyKit.Render-shaped engines use <see cref="Input"/> / <see cref="Output"/> / <see cref="Body"/>.
/// Exotic engines may treat <see cref="Body"/> as an opaque payload.
/// </summary>
public sealed class RenderRequest
{
    public required string Input { get; init; }

    public required string Output { get; init; }

    public required JsonElement Body { get; init; }
}

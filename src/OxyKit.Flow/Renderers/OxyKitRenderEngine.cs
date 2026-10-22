using OxyKit.Flow.Abstractions;
using OxyKit.Render.Client;

namespace OxyKit.Flow.Renderers;

/// <summary>
/// Built-in engine: OxyKit.Render sidecar via <see cref="RenderClient"/> (never Core).
/// Same <see cref="IDocumentRenderer"/> surface as third-party adapters.
/// </summary>
public sealed class OxyKitRenderEngine(string name, RenderClient client) : IDocumentRenderer
{
    public string Name { get; } = name;

    public async Task<Artifact> RenderAsync(
        RenderRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var file = await client.RenderAsync(
            request.Input,
            request.Output,
            request.Body,
            cancellationToken
        );

        return new Artifact
        {
            Content = file.Content,
            ContentType = file.ContentType,
            FileName = file.FileName,
        };
    }
}

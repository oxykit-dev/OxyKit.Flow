using OxyKit.Flow.Abstractions;

namespace OxyKit.Flow.Tests;

/// <summary>Stand-in third-party engine used to exercise Kind=assembly loading.</summary>
public sealed class TestPluginDocumentRenderer : IDocumentRenderer
{
    public string Name => "test-plugin";

    public Task<Artifact> RenderAsync(
        RenderRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(request.Body.GetRawText());
        return Task.FromResult(
            new Artifact
            {
                Content = bytes,
                ContentType = "application/json",
                FileName = "plugin.json",
            }
        );
    }
}

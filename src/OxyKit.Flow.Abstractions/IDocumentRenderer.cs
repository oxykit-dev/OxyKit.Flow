namespace OxyKit.Flow.Abstractions;

/// <summary>
/// Named render engine. Job <c>render.engine</c> matches <see cref="Name"/>.
/// Implement in a separate assembly and register via <c>Render:Engines</c> (<c>Kind=assembly</c>)
/// or DI — do not fork the Flow host.
/// </summary>
public interface IDocumentRenderer
{
    string Name { get; }

    Task<Artifact> RenderAsync(
        RenderRequest request,
        CancellationToken cancellationToken = default
    );
}

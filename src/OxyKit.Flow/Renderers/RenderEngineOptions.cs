namespace OxyKit.Flow.Renderers;

public sealed class RenderEngineOptions
{
    public const string SectionName = "Render";

    /// <summary>
    /// Named engines keyed by <c>IDocumentRenderer.Name</c>
    /// (e.g. <c>oxykit-render</c>).
    /// </summary>
    public Dictionary<string, RenderEngineEndpointOptions> Engines { get; init; } = new(
        StringComparer.OrdinalIgnoreCase
    );
}

public sealed class RenderEngineEndpointOptions
{
    /// <summary>
    /// <c>oxykit</c> (default) or <c>assembly</c>.
    /// </summary>
    public string? Kind { get; init; }

    public string? BaseAddress { get; init; }

    public string? ApiKey { get; init; }

    /// <summary>Path to plugin DLL (absolute or relative to content root / base directory).</summary>
    public string? Assembly { get; init; }

    /// <summary>Fully qualified type implementing <c>IDocumentRenderer</c>.</summary>
    public string? Type { get; init; }
}

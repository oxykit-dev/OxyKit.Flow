namespace OxyKit.Flow.Abstractions;

public static class DocumentRenderers
{
    /// <summary>Default engine when <c>render.engine</c> is omitted.</summary>
    public const string DefaultEngineName = "oxykit-render";

    /// <summary>Built-in OxyKit.Render Client adapter.</summary>
    public const string KindOxyKit = "oxykit";

    /// <summary>Load <see cref="IDocumentRenderer"/> from Assembly + Type.</summary>
    public const string KindAssembly = "assembly";
}

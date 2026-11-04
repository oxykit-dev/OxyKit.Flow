namespace OxyKit.Flow.Services;

public sealed class ArtifactCacheOptions
{
    public const string SectionName = "Artifacts";

    /// <summary>Directory root for per-job artifact files. Default <c>data/artifacts</c>.</summary>
    public string Root { get; set; } = "data/artifacts";
}

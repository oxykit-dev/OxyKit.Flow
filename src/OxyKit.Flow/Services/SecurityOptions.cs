namespace OxyKit.Flow.Services;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string ApiKeyHeader { get; set; } = "X-OxyKit-Flow-Key";

    /// <summary>Shared secret; required at startup (except health).</summary>
    public string ApiKey { get; set; } = "";
}

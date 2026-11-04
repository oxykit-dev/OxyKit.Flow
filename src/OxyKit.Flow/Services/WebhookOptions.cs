namespace OxyKit.Flow.Services;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    /// <summary>
    /// Fallback URL when a job has no <c>webhookUrl</c>. Absolute http(s) only.
    /// </summary>
    public string? DefaultUrl { get; set; }

    /// <summary>
    /// Optional HMAC-SHA256 secret. When set, posts include
    /// <c>X-OxyKit-Flow-Signature: sha256=&lt;hex&gt;</c> over the raw body.
    /// </summary>
    public string? SigningSecret { get; set; }

    /// <summary>HTTP timeout for webhook POSTs (best-effort). Default 5 seconds.</summary>
    public int TimeoutSeconds { get; set; } = 5;
}

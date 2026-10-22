namespace OxyKit.Flow.Outputs;

/// <summary>
/// Settings for delivering artifacts through OxyKit.Mail (<c>POST /api/v1/email/send</c>).
/// </summary>
public sealed class MailOutputOptions
{
    public const string SectionName = "OxyKit.Mail";

    /// <summary>Mail API base URL, e.g. http://localhost:5026/</summary>
    public Uri BaseAddress { get; set; } = new("http://localhost:5026/");

    /// <summary>API key for <c>X-Api-Key</c> (Sender or Admin).</summary>
    public string ApiKey { get; set; } = "";

    public string ApiKeyHeader { get; set; } = "X-Api-Key";

    /// <summary>
    /// Subject template. <c>{fileName}</c> and <c>{workflow}</c> are replaced when known.
    /// </summary>
    public string DefaultSubject { get; set; } = "Report: {fileName}";

    public string DefaultBody { get; set; } = "Please find the attached report.";

    public bool DefaultIsHtml { get; set; }

    public string? DefaultCc { get; set; }

    public string? DefaultBcc { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}

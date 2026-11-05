using System.Text.Json;

namespace OxyKit.Flow.Outputs;

/// <summary>
/// One delivery target for an artifact.
/// </summary>
public sealed class DeliveryRequest
{
    /// <summary><see cref="IOutputProvider.Name"/>, e.g. <c>file</c>, <c>mail</c>, <c>ftp</c>, <c>sftp</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>
    /// File path, mail To address, or remote FTP/SFTP path.
    /// For <c>ftp</c>/<c>sftp</c>, if this ends with <c>/</c> or has no file name, the artifact file name is used.
    /// </summary>
    public required string Destination { get; init; }

    /// <summary>
    /// Named connection profile under <c>Outputs:Transfer:Profiles</c>.
    /// Required for <c>ftp</c> and <c>sftp</c>.
    /// </summary>
    public string? Profile { get; init; }

    /// <summary>Optional mail subject override (ignored when <see cref="TemplateId"/> is set).</summary>
    public string? Subject { get; init; }

    /// <summary>Optional mail body override (ignored when <see cref="TemplateId"/> is set).</summary>
    public string? Body { get; init; }

    /// <summary>
    /// Optional OxyKit.Mail template id. When set, Mail renders <c>subject.txt</c> / <c>body.html</c>
    /// with <see cref="Data"/>; Flow still attaches the render artifact.
    /// </summary>
    public string? TemplateId { get; init; }

    /// <summary>JSON object for <see cref="TemplateId"/> (required by Mail when templateId is set).</summary>
    public JsonElement? Data { get; init; }

    /// <summary>
    /// When true, wrap the single render artifact in a zip before this provider runs
    /// (<c>file</c> / <c>mail</c> / <c>ftp</c> / <c>sftp</c>). Not a separate provider.
    /// Path destinations that name a file are rewritten to <c>.zip</c> when needed.
    /// </summary>
    public bool Zip { get; init; }
}

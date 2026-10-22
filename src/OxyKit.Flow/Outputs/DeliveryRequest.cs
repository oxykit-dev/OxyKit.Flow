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

    /// <summary>Optional mail subject override.</summary>
    public string? Subject { get; init; }

    /// <summary>Optional mail body override.</summary>
    public string? Body { get; init; }
}

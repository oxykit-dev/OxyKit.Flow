namespace OxyKit.Flow.Outputs;

/// <summary>
/// Named FTP/SFTP connection profiles for transfer output providers.
/// </summary>
public sealed class TransferOutputOptions
{
    public const string SectionName = "Outputs:Transfer";

    /// <summary>Profile name → connection settings. Names are globally unique across protocols.</summary>
    public Dictionary<string, TransferProfileOptions> Profiles { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>One named FTP or SFTP endpoint.</summary>
public sealed class TransferProfileOptions
{
    /// <summary><c>ftp</c> or <c>sftp</c>.</summary>
    public string Protocol { get; set; } = "";

    public string Host { get; set; } = "";

    /// <summary>0 = protocol default (21 FTP, 22 SFTP).</summary>
    public int Port { get; set; }

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary>Optional remote directory prepended to job destination.</summary>
    public string? RemotePathPrefix { get; set; }

    // --- FTP / FTPS ---

    /// <summary>When true, use FTPS (explicit TLS). Ignored for SFTP.</summary>
    public bool UseSsl { get; set; }

    /// <summary>Dev-only: accept any TLS certificate for FTPS. Do not enable in production.</summary>
    public bool AcceptAnyTlsCertificate { get; set; }

    // --- SFTP ---

    /// <summary>Path to private key file (password auth used when empty).</summary>
    public string? PrivateKeyPath { get; set; }

    public string? PrivateKeyPassphrase { get; set; }

    /// <summary>
    /// Optional SHA-256 or MD5 host key fingerprint. When set, mismatch fails the upload.
    /// When empty, host key is accepted (document risk for non-dev).
    /// </summary>
    public string? HostKeyFingerprint { get; set; }
}

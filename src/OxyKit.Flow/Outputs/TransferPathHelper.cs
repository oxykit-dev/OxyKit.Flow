namespace OxyKit.Flow.Outputs;

/// <summary>Shared remote-path resolution for FTP/SFTP outputs.</summary>
internal static class TransferPathHelper
{
    public static TransferProfileOptions ResolveProfile(
        TransferOutputOptions options,
        string? profileName,
        string expectedProtocol
    )
    {
        if (string.IsNullOrWhiteSpace(profileName))
            throw new ArgumentException(
                $"Output provider '{expectedProtocol}' requires a profile name."
            );

        var key = profileName.Trim();
        // Config binder may use ordinal keys; match case-insensitively.
        var profile = options
            .Profiles.FirstOrDefault(kv =>
                string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)
            )
            .Value;
        if (profile is null)
            throw new ArgumentException($"Unknown transfer profile '{key}'.");

        if (
            !string.Equals(
                profile.Protocol.Trim(),
                expectedProtocol,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new ArgumentException(
                $"Transfer profile '{key}' is protocol '{profile.Protocol}', but provider is '{expectedProtocol}'."
            );
        }

        if (string.IsNullOrWhiteSpace(profile.Host))
            throw new InvalidOperationException($"Transfer profile '{key}' has no Host.");
        if (string.IsNullOrWhiteSpace(profile.Username))
            throw new InvalidOperationException($"Transfer profile '{key}' has no Username.");

        return profile;
    }

    /// <summary>
    /// Combines optional profile prefix with job destination and artifact file name.
    /// Rejects <c>..</c> segments.
    /// </summary>
    public static string ResolveRemotePath(
        TransferProfileOptions profile,
        string destination,
        string? artifactFileName
    )
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination (remote path) is required.");

        var dest = destination.Trim().Replace('\\', '/');
        EnsureNoDotDot(dest);

        var fileName = string.IsNullOrWhiteSpace(artifactFileName)
            ? "report.bin"
            : artifactFileName.Trim();

        // Directory-only destination → append artifact file name
        if (dest.EndsWith('/') || string.IsNullOrEmpty(Path.GetFileName(dest)))
        {
            dest = dest.TrimEnd('/') + "/" + fileName;
        }

        var prefix = string.IsNullOrWhiteSpace(profile.RemotePathPrefix)
            ? null
            : profile.RemotePathPrefix.Trim().Replace('\\', '/').TrimEnd('/');

        if (prefix is not null)
            EnsureNoDotDot(prefix);

        string full;
        if (prefix is null || prefix.Length == 0)
        {
            full = dest.StartsWith('/') ? dest : "/" + dest;
        }
        else if (dest.StartsWith('/'))
        {
            // Absolute destination ignores prefix (still under server root)
            full = dest;
        }
        else
        {
            full = prefix.StartsWith('/')
                ? prefix + "/" + dest.TrimStart('/')
                : "/" + prefix.TrimStart('/') + "/" + dest.TrimStart('/');
        }

        EnsureNoDotDot(full);
        return NormalizeSlashes(full);
    }

    public static string ParentDirectory(string remotePath)
    {
        var normalized = NormalizeSlashes(remotePath);
        var idx = normalized.LastIndexOf('/');
        if (idx <= 0)
            return "/";
        return normalized[..idx];
    }

    private static void EnsureNoDotDot(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p == ".."))
            throw new ArgumentException("Remote path must not contain '..' segments.");
    }

    private static string NormalizeSlashes(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return "/" + string.Join('/', parts);
    }
}

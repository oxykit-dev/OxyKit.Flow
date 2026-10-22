using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace OxyKit.Flow.Outputs;

/// <summary>
/// Delivers an <see cref="Artifact"/> via SFTP using a named transfer profile.
/// </summary>
public sealed class SftpOutputProvider(
    IOptions<TransferOutputOptions> options,
    ILogger<SftpOutputProvider> logger
) : IOutputProvider
{
    public string Name => "sftp";

    public async Task SendAsync(
        Artifact artifact,
        DeliveryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var profile = TransferPathHelper.ResolveProfile(options.Value, request.Profile, Name);
        var remotePath = TransferPathHelper.ResolveRemotePath(
            profile,
            request.Destination,
            artifact.FileName
        );
        var port = profile.Port > 0 ? profile.Port : 22;

        var authMethods = BuildAuthMethods(profile);
        var connection = new Renci.SshNet.ConnectionInfo(
            profile.Host.Trim(),
            port,
            profile.Username.Trim(),
            authMethods
        );

        using var client = new SftpClient(connection);

        if (!string.IsNullOrWhiteSpace(profile.HostKeyFingerprint))
        {
            var expected = NormalizeFingerprint(profile.HostKeyFingerprint);
            client.HostKeyReceived += (_, e) =>
            {
                var sha256 = Convert.ToHexString(SHA256.HashData(e.HostKey)).ToLowerInvariant();
                var md5 = Convert.ToHexString(MD5.HashData(e.HostKey)).ToLowerInvariant();
                e.CanTrust =
                    string.Equals(sha256, expected, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(md5, expected, StringComparison.OrdinalIgnoreCase)
                    || FingerprintMatchesBase64Sha256(e.HostKey, expected);
            };
        }
        else
        {
            client.HostKeyReceived += (_, e) => e.CanTrust = true;
        }

        try
        {
            await Task.Run(() => client.Connect(), cancellationToken);

            var parent = TransferPathHelper.ParentDirectory(remotePath);
            EnsureRemoteDirectory(client, parent);

            await using var stream = new MemoryStream(artifact.Content, writable: false);
            await Task.Run(
                () => client.UploadFile(stream, remotePath, canOverride: true),
                cancellationToken
            );

            logger.LogInformation(
                "SFTP uploaded {Bytes} bytes via profile {Profile} → {RemotePath}",
                artifact.Content.Length,
                request.Profile,
                remotePath
            );
        }
        catch (SshAuthenticationException ex)
        {
            throw new InvalidOperationException(
                $"SFTP authentication failed for profile '{request.Profile}': {ex.Message}",
                ex
            );
        }
        finally
        {
            if (client.IsConnected)
                client.Disconnect();
        }
    }

    private static AuthenticationMethod[] BuildAuthMethods(TransferProfileOptions profile)
    {
        var methods = new List<AuthenticationMethod>();
        var user = profile.Username.Trim();

        if (!string.IsNullOrWhiteSpace(profile.PrivateKeyPath))
        {
            var keyPath = profile.PrivateKeyPath.Trim();
            if (!File.Exists(keyPath))
            {
                throw new InvalidOperationException(
                    $"SFTP private key file not found: '{keyPath}'."
                );
            }

            PrivateKeyFile keyFile = string.IsNullOrEmpty(profile.PrivateKeyPassphrase)
                ? new PrivateKeyFile(keyPath)
                : new PrivateKeyFile(keyPath, profile.PrivateKeyPassphrase);
            methods.Add(new PrivateKeyAuthenticationMethod(user, keyFile));
        }

        if (!string.IsNullOrEmpty(profile.Password) || methods.Count == 0)
            methods.Add(new PasswordAuthenticationMethod(user, profile.Password ?? ""));

        return methods.ToArray();
    }

    private static void EnsureRemoteDirectory(SftpClient client, string directory)
    {
        if (string.IsNullOrEmpty(directory) || directory == "/")
            return;

        var parts = directory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "";
        foreach (var part in parts)
        {
            current += "/" + part;
            if (!client.Exists(current))
                client.CreateDirectory(current);
        }
    }

    private static string NormalizeFingerprint(string fingerprint)
    {
        var s = fingerprint.Trim();
        if (s.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
            s = s["SHA256:".Length..];
        else if (s.StartsWith("MD5:", StringComparison.OrdinalIgnoreCase))
            s = s["MD5:".Length..];

        return s.Replace(":", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .Trim();
    }

    private static bool FingerprintMatchesBase64Sha256(byte[] hostKey, string expected)
    {
        // OpenSSH-style: SHA256:<base64 without padding>
        var b64 = Convert.ToBase64String(SHA256.HashData(hostKey)).TrimEnd('=');
        var expectedB64 = expected.TrimEnd('=');
        return string.Equals(b64, expectedB64, StringComparison.Ordinal);
    }
}

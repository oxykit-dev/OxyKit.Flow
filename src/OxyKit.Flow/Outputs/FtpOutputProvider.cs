using FluentFTP;
using FluentFTP.Exceptions;
using Microsoft.Extensions.Options;

namespace OxyKit.Flow.Outputs;

/// <summary>
/// Delivers an <see cref="Artifact"/> via classic FTP or FTPS using a named transfer profile.
/// </summary>
public sealed class FtpOutputProvider(
    IOptions<TransferOutputOptions> options,
    ILogger<FtpOutputProvider> logger
) : IOutputProvider
{
    public string Name => "ftp";

    public async Task<DeliveryResult> SendAsync(
        Artifact artifact,
        DeliveryRequest request,
        DeliveryContext context,
        CancellationToken cancellationToken = default
    )
    {
        var profile = TransferPathHelper.ResolveProfile(options.Value, request.Profile, Name);
        var remotePath = TransferPathHelper.ResolveRemotePath(
            profile,
            request.Destination,
            artifact.FileName
        );
        var port = profile.Port > 0 ? profile.Port : 21;

        using var client = new AsyncFtpClient(
            profile.Host.Trim(),
            profile.Username.Trim(),
            profile.Password ?? "",
            port
        );

        if (profile.UseSsl)
        {
            client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
            client.Config.ValidateAnyCertificate = profile.AcceptAnyTlsCertificate;
        }

        try
        {
            await client.Connect(cancellationToken);

            var parent = TransferPathHelper.ParentDirectory(remotePath);
            if (parent is not "/" and not "")
                await client.CreateDirectory(parent, true, cancellationToken);

            var partialPath = remotePath + ".partial";
            await using var stream = new MemoryStream(artifact.Content, writable: false);
            var status = await client.UploadStream(
                stream,
                partialPath,
                FtpRemoteExists.Overwrite,
                createRemoteDir: false,
                token: cancellationToken
            );

            if (status is FtpStatus.Failed)
            {
                throw new InvalidOperationException(
                    $"FTP upload failed for profile '{request.Profile}' → {partialPath}."
                );
            }

            // Rename into place so a crash mid-upload leaves *.partial, not a half final file.
            if (await client.FileExists(remotePath, cancellationToken))
                await client.DeleteFile(remotePath, cancellationToken);
            await client.Rename(partialPath, remotePath, token: cancellationToken);

            logger.LogInformation(
                "FTP uploaded {Bytes} bytes via profile {Profile} → {RemotePath} (status={Status})",
                artifact.Content.Length,
                request.Profile,
                remotePath,
                status
            );
        }
        catch (FtpAuthenticationException ex)
        {
            throw new InvalidOperationException(
                $"FTP authentication failed for profile '{request.Profile}': {ex.Message}",
                ex
            );
        }
        finally
        {
            if (client.IsConnected)
                await client.Disconnect(cancellationToken);
        }

        return DeliveryResult.Empty;
    }
}

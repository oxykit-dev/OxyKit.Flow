using System.IO.Compression;

namespace OxyKit.Flow.Outputs;

/// <summary>
/// Optional per-delivery packaging: wrap the single render artifact in a zip before the real provider runs.
/// Not an <see cref="IOutputProvider"/> — password-protected and multi-entry archives are out of scope.
/// </summary>
public static class ArtifactZipPackager
{
    public const string ZipContentType = "application/zip";

    /// <summary>
    /// When <see cref="DeliveryRequest.Zip"/> is true, returns a new artifact whose content is a
    /// single-entry zip of <paramref name="artifact"/>; otherwise returns <paramref name="artifact"/>.
    /// </summary>
    public static Artifact PackIfRequested(Artifact artifact, DeliveryRequest request)
    {
        if (!request.Zip)
            return artifact;

        var entryName = ResolveEntryName(artifact);
        var zipFileName = Path.ChangeExtension(entryName, ".zip") ?? $"{entryName}.zip";

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(artifact.Content);
        }

        return new Artifact
        {
            Content = zipStream.ToArray(),
            ContentType = ZipContentType,
            FileName = zipFileName,
        };
    }

    /// <summary>
    /// For path-based destinations (<c>file</c>/<c>ftp</c>/<c>sftp</c>), ensure a file destination
    /// ends with <c>.zip</c> so bytes match the path. Directory destinations and mail To addresses
    /// are left unchanged (artifact <see cref="Artifact.FileName"/> carries the zip name).
    /// </summary>
    public static DeliveryRequest AdjustDestination(DeliveryRequest request)
    {
        if (!request.Zip)
            return request;

        if (request.Provider.Equals("mail", StringComparison.OrdinalIgnoreCase))
            return request;

        var dest = request.Destination.Trim().Replace('\\', '/');
        if (dest.EndsWith('/') || string.IsNullOrEmpty(Path.GetFileName(dest)))
            return request;

        if (dest.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return request;

        var withZip = Path.ChangeExtension(dest, ".zip") ?? dest + ".zip";
        // Path.ChangeExtension may flip separators on some OS — keep forward slashes for remote paths.
        withZip = withZip.Replace('\\', '/');

        return new DeliveryRequest
        {
            Provider = request.Provider,
            Destination = withZip,
            Profile = request.Profile,
            Subject = request.Subject,
            Body = request.Body,
            Zip = request.Zip,
        };
    }

    private static string ResolveEntryName(Artifact artifact)
    {
        if (!string.IsNullOrWhiteSpace(artifact.FileName))
        {
            var name = Path.GetFileName(artifact.FileName.Trim().Replace('\\', '/'));
            if (!string.IsNullOrWhiteSpace(name) && name is not "." and not "..")
                return name;
        }

        return GuessEntryName(artifact.ContentType);
    }

    private static string GuessEntryName(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "application/pdf" => "report.pdf",
            "text/html" => "report.html",
            "text/csv" => "report.csv",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "report.xlsx",
            _ => "report.bin",
        };
}

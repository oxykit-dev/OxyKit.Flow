namespace OxyKit.Flow.Outputs;

/// <summary>
/// Writes the artifact to disk via a temp <c>*.partial</c> file then rename,
/// so a crash mid-write does not leave a final path that retries would duplicate beside.
/// </summary>
public sealed class FileOutputProvider : IOutputProvider
{
    public string Name => "file";

    public async Task<DeliveryResult> SendAsync(
        Artifact artifact,
        DeliveryRequest request,
        DeliveryContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Destination))
            throw new ArgumentException("File destination (path) is required.", nameof(request));

        var destination = request.Destination.Trim();
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var partial = destination + ".partial";
        try
        {
            await File.WriteAllBytesAsync(partial, artifact.Content, cancellationToken);
            // Same-volume replace: atomic on typical local FS; retries overwrite the final path.
            File.Move(partial, destination, overwrite: true);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }

        return DeliveryResult.Empty;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

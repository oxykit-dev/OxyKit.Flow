namespace OxyKit.Flow.Outputs;

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

        await File.WriteAllBytesAsync(destination, artifact.Content, cancellationToken);
        return DeliveryResult.Empty;
    }
}

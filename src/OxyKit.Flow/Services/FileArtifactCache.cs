using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OxyKit.Flow.Services;

public sealed class FileArtifactCache(IOptions<ArtifactCacheOptions> options, ILogger<FileArtifactCache> logger)
    : IArtifactCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task<Artifact?> TryGetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var dir = JobDir(jobId);
        var metaPath = Path.Combine(dir, "meta.json");
        var binPath = Path.Combine(dir, "artifact.bin");
        if (!File.Exists(metaPath) || !File.Exists(binPath))
            return null;

        try
        {
            await using var metaStream = File.OpenRead(metaPath);
            var meta = await JsonSerializer.DeserializeAsync<ArtifactMeta>(
                metaStream,
                JsonOptions,
                cancellationToken
            );
            if (meta is null || string.IsNullOrWhiteSpace(meta.ContentType))
                return null;

            var bytes = await File.ReadAllBytesAsync(binPath, cancellationToken);
            return new Artifact
            {
                Content = bytes,
                ContentType = meta.ContentType,
                FileName = meta.FileName,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read artifact cache for job {JobId}", jobId);
            return null;
        }
    }

    public async Task StoreAsync(
        Guid jobId,
        Artifact artifact,
        CancellationToken cancellationToken = default
    )
    {
        var dir = JobDir(jobId);
        Directory.CreateDirectory(dir);

        var metaPath = Path.Combine(dir, "meta.json");
        var binPath = Path.Combine(dir, "artifact.bin");
        var partialBin = binPath + ".partial";
        var partialMeta = metaPath + ".partial";

        await File.WriteAllBytesAsync(partialBin, artifact.Content, cancellationToken);
        await using (var metaStream = File.Create(partialMeta))
        {
            await JsonSerializer.SerializeAsync(
                metaStream,
                new ArtifactMeta
                {
                    ContentType = artifact.ContentType,
                    FileName = artifact.FileName,
                },
                JsonOptions,
                cancellationToken
            );
        }

        File.Move(partialBin, binPath, overwrite: true);
        File.Move(partialMeta, metaPath, overwrite: true);
    }

    public Task DeleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dir = JobDir(jobId);
        if (!Directory.Exists(dir))
            return Task.CompletedTask;

        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete artifact cache for job {JobId}", jobId);
        }

        return Task.CompletedTask;
    }

    private string JobDir(Guid jobId)
    {
        var root = options.Value.Root;
        if (string.IsNullOrWhiteSpace(root))
            root = "data/artifacts";
        return Path.Combine(root, jobId.ToString("N"));
    }

    private sealed class ArtifactMeta
    {
        public required string ContentType { get; init; }
        public string? FileName { get; init; }
    }
}

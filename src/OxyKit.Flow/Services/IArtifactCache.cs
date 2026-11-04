namespace OxyKit.Flow.Services;

/// <summary>
/// Filesystem cache of a job's render artifact so delivery-only retries skip Render.
/// </summary>
public interface IArtifactCache
{
    Task<Artifact?> TryGetAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task StoreAsync(Guid jobId, Artifact artifact, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid jobId, CancellationToken cancellationToken = default);
}

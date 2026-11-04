using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxyKit.Flow.Services;

namespace OxyKit.Flow.Tests;

public sealed class FileArtifactCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"oxykit-flow-cache-{Guid.NewGuid():N}"
    );

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public async Task Store_Then_TryGet_RoundTrips()
    {
        var cache = new FileArtifactCache(
            Options.Create(new ArtifactCacheOptions { Root = _root }),
            NullLogger<FileArtifactCache>.Instance
        );
        var jobId = Guid.NewGuid();
        var original = new Artifact
        {
            Content = "hello"u8.ToArray(),
            ContentType = "text/plain",
            FileName = "hi.txt",
        };

        await cache.StoreAsync(jobId, original, TestContext.Current.CancellationToken);
        var loaded = await cache.TryGetAsync(jobId, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.ContentType.Should().Be("text/plain");
        loaded.FileName.Should().Be("hi.txt");
        loaded.Content.Should().Equal(original.Content);
    }

    [Fact]
    public async Task Delete_RemovesFiles()
    {
        var cache = new FileArtifactCache(
            Options.Create(new ArtifactCacheOptions { Root = _root }),
            NullLogger<FileArtifactCache>.Instance
        );
        var jobId = Guid.NewGuid();
        await cache.StoreAsync(
            jobId,
            new Artifact
            {
                Content = [1, 2, 3],
                ContentType = "application/octet-stream",
            },
            TestContext.Current.CancellationToken
        );

        await cache.DeleteAsync(jobId, TestContext.Current.CancellationToken);
        var loaded = await cache.TryGetAsync(jobId, TestContext.Current.CancellationToken);
        loaded.Should().BeNull();
    }
}

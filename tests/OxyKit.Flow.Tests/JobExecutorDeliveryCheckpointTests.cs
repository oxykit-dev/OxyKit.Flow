using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxyKit.Flow.Infrastructure;
using OxyKit.Flow.Outputs;
using OxyKit.Flow.Services;

namespace OxyKit.Flow.Tests;

public sealed class JobExecutorDeliveryCheckpointTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"oxykit-flow-exec-{Guid.NewGuid():N}.db"
    );
    private readonly string _artifactRoot = Path.Combine(
        Path.GetTempPath(),
        $"oxykit-flow-artifacts-{Guid.NewGuid():N}"
    );

    private FlowDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FlowDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var db = new FlowDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private IArtifactCache CreateCache() =>
        new FileArtifactCache(
            Options.Create(new ArtifactCacheOptions { Root = _artifactRoot }),
            NullLogger<FileArtifactCache>.Instance
        );

    private JobExecutor CreateExecutor(
        FlowDbContext db,
        IEnumerable<IDocumentRenderer> engines,
        IEnumerable<IOutputProvider> outputs
    ) => new(db, engines, outputs, CreateCache(), NullLogger<JobExecutor>.Instance);

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
            // best-effort
        }

        try
        {
            if (Directory.Exists(_artifactRoot))
                Directory.Delete(_artifactRoot, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public async Task ExecuteAsync_AllSucceeded_SkipsRender()
    {
        await using var db = CreateDb();
        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Jobs.Add(
            new Job
            {
                Id = jobId,
                Status = JobStatus.Processing,
                RenderEngine = "stub",
                RenderInput = "table",
                RenderOutput = "html",
                RenderBodyJson = "{}",
                OutputsJson = """[{"provider":"a","destination":"one"}]""",
                AttemptCount = 2,
                MaxAttempts = 3,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        db.JobDeliveries.Add(
            new JobDelivery
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Ordinal = 0,
                Provider = "a",
                Destination = "one",
                Status = JobDeliveryStatus.Succeeded,
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = now,
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var renderer = new CountingRenderer();
        var provider = new CountingProvider("a");
        var executor = CreateExecutor(db, [renderer], [provider]);

        var job = await db.Jobs.SingleAsync(
            j => j.Id == jobId,
            TestContext.Current.CancellationToken
        );
        await executor.ExecuteAsync(job, TestContext.Current.CancellationToken);

        renderer.RenderCount.Should().Be(0);
        provider.SendCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsSucceededDelivery_OnRetry()
    {
        await using var db = CreateDb();
        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Jobs.Add(
            new Job
            {
                Id = jobId,
                Status = JobStatus.Processing,
                RenderEngine = "stub",
                RenderInput = "table",
                RenderOutput = "html",
                RenderBodyJson = "{}",
                OutputsJson =
                    """[{"provider":"a","destination":"one"},{"provider":"b","destination":"two"}]""",
                AttemptCount = 2,
                MaxAttempts = 3,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        db.JobDeliveries.AddRange(
            new JobDelivery
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Ordinal = 0,
                Provider = "a",
                Destination = "one",
                Status = JobDeliveryStatus.Succeeded,
                CreatedAt = now,
                UpdatedAt = now,
                CompletedAt = now,
            },
            new JobDelivery
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Ordinal = 1,
                Provider = "b",
                Destination = "two",
                Status = JobDeliveryStatus.Failed,
                LastError = "boom",
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var renderer = new CountingRenderer();
        var providerA = new CountingProvider("a");
        var providerB = new CountingProvider("b");
        var executor = CreateExecutor(db, [renderer], [providerA, providerB]);

        var job = await db.Jobs.SingleAsync(
            j => j.Id == jobId,
            TestContext.Current.CancellationToken
        );
        await executor.ExecuteAsync(job, TestContext.Current.CancellationToken);

        providerA.SendCount.Should().Be(0);
        providerB.SendCount.Should().Be(1);
        renderer.RenderCount.Should().Be(1);

        var deliveries = await db
            .JobDeliveries.OrderBy(d => d.Ordinal)
            .ToListAsync(TestContext.Current.CancellationToken);
        deliveries[0].Status.Should().Be(JobDeliveryStatus.Succeeded);
        deliveries[1].Status.Should().Be(JobDeliveryStatus.Succeeded);

        // Delivery-only retry should hit filesystem cache, not Render again.
        deliveries[1].MarkAsPendingForRetry();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        job = await db.Jobs.SingleAsync(j => j.Id == jobId, TestContext.Current.CancellationToken);
        await executor.ExecuteAsync(job, TestContext.Current.CancellationToken);

        renderer.RenderCount.Should().Be(1);
        providerB.SendCount.Should().Be(2);
        job.ArtifactContentType.Should().Be("text/html");
        job.ArtifactCachedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ZipTrue_PassesZippedArtifactAndAdjustedPath()
    {
        await using var db = CreateDb();
        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Jobs.Add(
            new Job
            {
                Id = jobId,
                Status = JobStatus.Processing,
                RenderEngine = "stub",
                RenderInput = "table",
                RenderOutput = "html",
                RenderBodyJson = "{}",
                OutputsJson =
                    """[{"provider":"a","destination":"output/report.html","zip":true}]""",
                AttemptCount = 1,
                MaxAttempts = 3,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        db.JobDeliveries.Add(
            new JobDelivery
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Ordinal = 0,
                Provider = "a",
                Destination = "output/report.html",
                Status = JobDeliveryStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new CapturingProvider("a");
        var executor = CreateExecutor(db, [new StubRenderer()], [provider]);

        var job = await db.Jobs.SingleAsync(
            j => j.Id == jobId,
            TestContext.Current.CancellationToken
        );
        await executor.ExecuteAsync(job, TestContext.Current.CancellationToken);

        provider.LastArtifact.Should().NotBeNull();
        provider.LastArtifact!.ContentType.Should().Be(ArtifactZipPackager.ZipContentType);
        provider.LastArtifact.FileName.Should().Be("out.zip");
        provider.LastRequest!.Destination.Should().Be("output/report.zip");
        provider.LastRequest.Zip.Should().BeTrue();

        using var zip = new System.IO.Compression.ZipArchive(
            new MemoryStream(provider.LastArtifact.Content),
            System.IO.Compression.ZipArchiveMode.Read
        );
        zip.Entries.Should().ContainSingle().Which.FullName.Should().Be("out.html");
    }

    [Fact]
    public async Task ExecuteAsync_ProviderMismatch_Throws()
    {
        await using var db = CreateDb();
        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Jobs.Add(
            new Job
            {
                Id = jobId,
                Status = JobStatus.Processing,
                RenderEngine = "stub",
                RenderInput = "table",
                RenderOutput = "html",
                RenderBodyJson = "{}",
                OutputsJson = """[{"provider":"a","destination":"one"}]""",
                AttemptCount = 1,
                MaxAttempts = 3,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        db.JobDeliveries.Add(
            new JobDelivery
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Ordinal = 0,
                Provider = "b",
                Destination = "one",
                Status = JobDeliveryStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var executor = CreateExecutor(
            db,
            [new StubRenderer()],
            [new CountingProvider("a"), new CountingProvider("b")]
        );

        var job = await db.Jobs.SingleAsync(
            j => j.Id == jobId,
            TestContext.Current.CancellationToken
        );
        var act = () => executor.ExecuteAsync(job, TestContext.Current.CancellationToken);
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not match outputs JSON*");
    }

    [Fact]
    public async Task ExecuteAsync_MarksFailed_WhenProviderThrows()
    {
        await using var db = CreateDb();
        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Jobs.Add(
            new Job
            {
                Id = jobId,
                Status = JobStatus.Processing,
                RenderEngine = "stub",
                RenderInput = "table",
                RenderOutput = "html",
                RenderBodyJson = "{}",
                OutputsJson =
                    """[{"provider":"ok","destination":"one"},{"provider":"bad","destination":"two"}]""",
                AttemptCount = 1,
                MaxAttempts = 3,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        db.JobDeliveries.AddRange(
            new JobDelivery
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Ordinal = 0,
                Provider = "ok",
                Destination = "one",
                Status = JobDeliveryStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new JobDelivery
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Ordinal = 1,
                Provider = "bad",
                Destination = "two",
                Status = JobDeliveryStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            }
        );
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var executor = CreateExecutor(
            db,
            [new StubRenderer()],
            [new CountingProvider("ok"), new ThrowingProvider("bad")]
        );

        var job = await db.Jobs.SingleAsync(
            j => j.Id == jobId,
            TestContext.Current.CancellationToken
        );
        var act = () => executor.ExecuteAsync(job, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("fail-b");

        var deliveries = await db
            .JobDeliveries.OrderBy(d => d.Ordinal)
            .ToListAsync(TestContext.Current.CancellationToken);
        deliveries[0].Status.Should().Be(JobDeliveryStatus.Succeeded);
        deliveries[1].Status.Should().Be(JobDeliveryStatus.Failed);
        deliveries[1].LastError.Should().Contain("fail-b");
    }

    private sealed class StubRenderer : IDocumentRenderer
    {
        public string Name => "stub";

        public Task<Artifact> RenderAsync(
            RenderRequest request,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new Artifact
                {
                    Content = "<html/>"u8.ToArray(),
                    ContentType = "text/html",
                    FileName = "out.html",
                }
            );
    }

    private sealed class CountingRenderer : IDocumentRenderer
    {
        public string Name => "stub";
        public int RenderCount { get; private set; }

        public Task<Artifact> RenderAsync(
            RenderRequest request,
            CancellationToken cancellationToken = default
        )
        {
            RenderCount++;
            return Task.FromResult(
                new Artifact
                {
                    Content = "<html/>"u8.ToArray(),
                    ContentType = "text/html",
                    FileName = "out.html",
                }
            );
        }
    }

    private sealed class CountingProvider(string name) : IOutputProvider
    {
        public string Name { get; } = name;
        public int SendCount { get; private set; }

        public Task<DeliveryResult> SendAsync(
            Artifact artifact,
            DeliveryRequest request,
            DeliveryContext context,
            CancellationToken cancellationToken = default
        )
        {
            SendCount++;
            return Task.FromResult(DeliveryResult.Empty);
        }
    }

    private sealed class CapturingProvider(string name) : IOutputProvider
    {
        public string Name { get; } = name;
        public Artifact? LastArtifact { get; private set; }
        public DeliveryRequest? LastRequest { get; private set; }

        public Task<DeliveryResult> SendAsync(
            Artifact artifact,
            DeliveryRequest request,
            DeliveryContext context,
            CancellationToken cancellationToken = default
        )
        {
            LastArtifact = artifact;
            LastRequest = request;
            return Task.FromResult(DeliveryResult.Empty);
        }
    }

    private sealed class ThrowingProvider(string name) : IOutputProvider
    {
        public string Name { get; } = name;

        public Task<DeliveryResult> SendAsync(
            Artifact artifact,
            DeliveryRequest request,
            DeliveryContext context,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("fail-b");
    }
}

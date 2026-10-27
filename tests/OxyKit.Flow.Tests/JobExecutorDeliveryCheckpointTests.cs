using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OxyKit.Flow.Abstractions;
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

    private FlowDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FlowDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var db = new FlowDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

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
        var executor = new JobExecutor(
            db,
            [renderer],
            [provider],
            NullLogger<JobExecutor>.Instance
        );

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

        var providerA = new CountingProvider("a");
        var providerB = new CountingProvider("b");
        var executor = new JobExecutor(
            db,
            [new StubRenderer()],
            [providerA, providerB],
            NullLogger<JobExecutor>.Instance
        );

        var job = await db.Jobs.SingleAsync(
            j => j.Id == jobId,
            TestContext.Current.CancellationToken
        );
        await executor.ExecuteAsync(job, TestContext.Current.CancellationToken);

        providerA.SendCount.Should().Be(0);
        providerB.SendCount.Should().Be(1);

        var deliveries = await db
            .JobDeliveries.OrderBy(d => d.Ordinal)
            .ToListAsync(TestContext.Current.CancellationToken);
        deliveries[0].Status.Should().Be(JobDeliveryStatus.Succeeded);
        deliveries[1].Status.Should().Be(JobDeliveryStatus.Succeeded);
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

        var executor = new JobExecutor(
            db,
            [new StubRenderer()],
            [new CountingProvider("a"), new CountingProvider("b")],
            NullLogger<JobExecutor>.Instance
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

        var executor = new JobExecutor(
            db,
            [new StubRenderer()],
            [new CountingProvider("ok"), new ThrowingProvider("bad")],
            NullLogger<JobExecutor>.Instance
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

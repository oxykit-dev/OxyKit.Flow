using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OxyKit.Flow.Common.Observability;
using OxyKit.Flow.Infrastructure;
using OxyKit.Observability;

namespace OxyKit.Flow.Services;

/// <summary>
/// Claims queued jobs, executes them, and handles retry / dead-letter (Mail-style).
/// </summary>
public sealed class JobQueueProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<JobOptions> options,
    ILogger<JobQueueProcessor> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));
        logger.LogInformation(
            "Job queue processor started (poll every {Seconds}s, batch {Batch})",
            interval.TotalSeconds,
            options.Value.BatchSize
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job queue poll failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDbContext>();
        var claims = scope.ServiceProvider.GetRequiredService<IJobClaimStore>();
        var executor = scope.ServiceProvider.GetRequiredService<JobExecutor>();
        var webhooks = scope.ServiceProvider.GetRequiredService<IJobWebhookDispatcher>();
        var artifactCache = scope.ServiceProvider.GetRequiredService<IArtifactCache>();
        var opts = options.Value;
        var timeout = TimeSpan.FromSeconds(Math.Max(30, opts.ProcessingTimeoutSeconds));

        await claims.ReclaimStuckAsync(timeout, cancellationToken);

        var batchSize = Math.Max(1, opts.BatchSize);
        var claimed = await claims.ClaimQueuedAsync(batchSize, cancellationToken);
        if (claimed.Count > 0)
            FlowTelemetry.JobsClaimed.Add(claimed.Count);

        foreach (var job in claimed)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            CorrelationContext.Current = correlationId;
            using var activity = FlowTelemetry.ActivitySource.StartActivity(
                "flow.job.process",
                ActivityKind.Internal
            );
            activity?.SetTag("correlation.id", correlationId);
            activity?.SetTag("job.id", job.Id.ToString("D"));
            activity?.SetTag("job.attempt", job.AttemptCount);

            using var logScope = logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId,
                    ["JobId"] = job.Id,
                }
            );

            var sw = Stopwatch.StartNew();
            var terminal = false;
            try
            {
                logger.LogInformation(
                    "Processing job {JobId} (attempt {Attempt}/{Max})",
                    job.Id,
                    job.AttemptCount,
                    job.MaxAttempts
                );

                await executor.ExecuteAsync(job, cancellationToken);
                job.MarkAsCompleted();
                await db.SaveChangesAsync(cancellationToken);
                FlowTelemetry.JobsSucceeded.Add(1);
                activity?.SetStatus(ActivityStatusCode.Ok);
                logger.LogInformation("Job {JobId} completed", job.Id);
                terminal = true;
            }
            catch (Exception ex)
            {
                FlowTelemetry.JobsFailed.Add(1);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(
                    ex,
                    "Job {JobId} failed on attempt {Attempt}",
                    job.Id,
                    job.AttemptCount
                );

                if (job.CanBeRetried())
                {
                    job.MarkAsQueuedForRetry(Truncate(ex.Message));
                    logger.LogWarning(
                        "Job {JobId} re-queued ({Attempt}/{Max})",
                        job.Id,
                        job.AttemptCount,
                        job.MaxAttempts
                    );
                    await db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    job.MarkAsDeadLettered(
                        $"Max attempts ({job.MaxAttempts}) exceeded. Last error: {Truncate(ex.Message)}"
                    );
                    logger.LogError("Job {JobId} dead-lettered", job.Id);
                    await db.SaveChangesAsync(cancellationToken);
                    terminal = true;
                }
            }
            finally
            {
                sw.Stop();
                FlowTelemetry.JobDurationMs.Record(sw.Elapsed.TotalMilliseconds);
            }

            // Side effects after a durable terminal status — never change job status again.
            if (terminal)
            {
                try
                {
                    var deliveries = await LoadDeliveriesAsync(db, job.Id, cancellationToken);
                    await webhooks.NotifyTerminalAsync(job, deliveries, cancellationToken);
                    if (job.Status == JobStatus.Completed)
                    {
                        await artifactCache.DeleteAsync(job.Id, cancellationToken);
                        ClearArtifactColumns(job);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Post-terminal side effects failed for job {JobId} (status unchanged)",
                        job.Id
                    );
                }
            }

            CorrelationContext.Current = null;
        }
    }

    private static async Task<IReadOnlyList<JobDelivery>> LoadDeliveriesAsync(
        FlowDbContext db,
        Guid jobId,
        CancellationToken cancellationToken
    ) =>
        await db
            .JobDeliveries.AsNoTracking()
            .Where(d => d.JobId == jobId)
            .OrderBy(d => d.Ordinal)
            .ToListAsync(cancellationToken);

    private static void ClearArtifactColumns(Job job)
    {
        job.ArtifactContentType = null;
        job.ArtifactFileName = null;
        job.ArtifactCachedAt = null;
        job.UpdatedAt = DateTime.UtcNow;
    }

    private static string Truncate(string message) =>
        message.Length <= 4000 ? message : message[..4000];
}

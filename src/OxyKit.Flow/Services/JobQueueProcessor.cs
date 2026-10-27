using Microsoft.Extensions.Options;
using OxyKit.Flow.Infrastructure;

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
        var opts = options.Value;
        var timeout = TimeSpan.FromSeconds(Math.Max(30, opts.ProcessingTimeoutSeconds));

        await claims.ReclaimStuckAsync(timeout, cancellationToken);

        var batchSize = Math.Max(1, opts.BatchSize);
        var claimed = await claims.ClaimQueuedAsync(batchSize, cancellationToken);

        foreach (var job in claimed)
        {
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
                logger.LogInformation("Job {JobId} completed", job.Id);
            }
            catch (Exception ex)
            {
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
                }
                else
                {
                    job.MarkAsDeadLettered(
                        $"Max attempts ({job.MaxAttempts}) exceeded. Last error: {Truncate(ex.Message)}"
                    );
                    logger.LogError("Job {JobId} dead-lettered", job.Id);
                }

                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static string Truncate(string message) =>
        message.Length <= 4000 ? message : message[..4000];
}

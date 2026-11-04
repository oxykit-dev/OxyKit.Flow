using OxyKit.Flow.Infrastructure;

namespace OxyKit.Flow.Services;

public interface IJobWebhookDispatcher
{
    /// <summary>
    /// Best-effort POST when a job reaches Completed or DeadLettered.
    /// Failures are logged only — never changes job status.
    /// </summary>
    Task NotifyTerminalAsync(
        Job job,
        IReadOnlyList<JobDelivery> deliveries,
        CancellationToken cancellationToken = default
    );
}

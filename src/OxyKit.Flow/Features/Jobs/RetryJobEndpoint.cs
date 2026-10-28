using OxyKit.Flow.Common;
using OxyKit.Flow.Features.Jobs.Services;

namespace OxyKit.Flow.Features.Jobs;

public sealed class RetryJobRequest
{
    public Guid Id { get; init; }
}

public sealed class RetryJobEndpoint(JobService jobs) : FlowEndpoint<RetryJobRequest, JobResponse>
{
    public override void Configure()
    {
        Post("/jobs/{id}/retry");
        Version(1);
        AuthSchemes(AuthSchemeNames.ApiKey);
        Summary(s =>
        {
            s.Summary = "Retry a dead-lettered job";
            s.Description =
                "Requeues a DeadLettered job with a fresh attempt budget. Succeeded deliveries stay skipped; "
                + "failed/pending deliveries are reset to Pending.";
        });
    }

    public override async Task HandleAsync(RetryJobRequest req, CancellationToken ct)
    {
        var result = await jobs.RetryDeadLetteredAsync(req.Id, ct);
        if (await SendOnErrorAsync(result, ct))
            return;

        var (job, deliveries) = result.Value;
        await Send.OkAsync(JobResponse.From(job, deliveries), cancellation: ct);
    }
}

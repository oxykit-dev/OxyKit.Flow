using OxyKit.Flow.Common;
using OxyKit.Flow.Features.Jobs.Services;

namespace OxyKit.Flow.Features.Jobs;

public sealed class GetJobRequest
{
    public Guid Id { get; init; }
}

public sealed class GetJobEndpoint(JobService jobs) : FlowEndpoint<GetJobRequest, JobResponse>
{
    public override void Configure()
    {
        Get("/jobs/{id}");
        Version(1);
        AuthSchemes(AuthSchemeNames.ApiKey);
        Summary(s =>
        {
            s.Summary = "Get a job by id";
        });
    }

    public override async Task HandleAsync(GetJobRequest req, CancellationToken ct)
    {
        var result = await jobs.GetAsync(req.Id, ct);
        if (await SendOnErrorAsync(result, ct))
            return;

        await Send.OkAsync(JobResponse.From(result.Value), cancellation: ct);
    }
}

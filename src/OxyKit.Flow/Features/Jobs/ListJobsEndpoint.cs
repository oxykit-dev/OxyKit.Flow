using OxyKit.Flow.Common;
using OxyKit.Flow.Features.Jobs.Services;

namespace OxyKit.Flow.Features.Jobs;

public sealed class ListJobsRequest
{
    /// <summary>Optional status filter (Queued, Processing, Completed, Failed, DeadLettered).</summary>
    public string? Status { get; init; }

    /// <summary>Max rows to return (default 50, max 100).</summary>
    public int? Limit { get; init; }
}

public sealed class ListJobsResponse
{
    public required IReadOnlyList<JobResponse> Items { get; init; }
    public required int Count { get; init; }
    public required int Total { get; init; }
}

public sealed class ListJobsEndpoint(JobService jobs) : FlowEndpoint<ListJobsRequest, ListJobsResponse>
{
    public override void Configure()
    {
        Get("/jobs/");
        Version(1);
        AuthSchemes(AuthSchemeNames.ApiKey);
        Summary(s =>
        {
            s.Summary = "List jobs";
            s.Description =
                "Returns newest jobs first. Optional query: status, limit (1–100, default 50). "
                + "total is the match count before limit; count is items returned.";
        });
    }

    public override async Task HandleAsync(ListJobsRequest req, CancellationToken ct)
    {
        var result = await jobs.ListAsync(req.Status, req.Limit, ct);
        if (await SendOnErrorAsync(result, ct))
            return;

        var body = new ListJobsResponse
        {
            Items = result
                .Value.Items.Select(x => JobResponse.From(x.Job, x.Deliveries))
                .ToList(),
            Count = result.Value.Count,
            Total = result.Value.Total,
        };
        await Send.OkAsync(body, cancellation: ct);
    }
}

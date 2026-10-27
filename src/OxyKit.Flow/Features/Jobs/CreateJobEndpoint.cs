using OxyKit.Flow.Common;
using OxyKit.Flow.Features.Jobs.Services;

namespace OxyKit.Flow.Features.Jobs;

public sealed class CreateJobEndpoint(JobService jobs)
    : FlowEndpoint<CreateJobRequest, JobResponse>
{
    public override void Configure()
    {
        Post("/jobs/");
        Version(1);
        AuthSchemes(AuthSchemeNames.ApiKey);
        Validator<CreateJobRequestValidator>();
        Summary(s =>
        {
            s.Summary = "Create a render+deliver job";
            s.Description =
                "Persists a queued job. Optional Idempotency-Key header returns the existing job (200) without creating a duplicate.";
        });
    }

    public override async Task HandleAsync(CreateJobRequest req, CancellationToken ct)
    {
        if (ValidationFailed)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var keyHeader);
        var idempotencyKey = keyHeader.ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            idempotencyKey = null;

        var result = await jobs.CreateAsync(
            req.Name,
            idempotencyKey,
            req.Render.Engine,
            req.Render.Input,
            req.Render.Output,
            req.Render.Body,
            req.Outputs,
            ct
        );
        if (await SendOnErrorAsync(result, ct))
            return;

        var body = JobResponse.From(result.Value.Job, result.Value.Deliveries);
        if (result.Value.Created)
            await Send.CreatedAtAsync<GetJobEndpoint>(new { id = body.Id }, body, cancellation: ct);
        else
            await Send.OkAsync(body, cancellation: ct);
    }
}

using System.Text.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OxyKit.Flow.Infrastructure;
using OxyKit.Flow.Outputs;
using OxyKit.Flow.Renderers;
using OxyKit.Flow.Services;

namespace OxyKit.Flow.Features.Jobs.Services;

public sealed class CreateJobResult
{
    public required Job Job { get; init; }
    public required bool Created { get; init; }
}

public sealed class JobService(
    FlowDbContext db,
    IOptions<JobOptions> options,
    TransferOutputValidator outputValidator,
    RenderEngineValidator engineValidator
)
{
    public async Task<ErrorOr<CreateJobResult>> CreateAsync(
        string? name,
        string? idempotencyKey,
        string? renderEngine,
        string renderInput,
        string renderOutput,
        JsonElement renderBody,
        IReadOnlyList<DeliveryRequest> outputs,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(renderInput))
            return Error.Validation(description: "render.input is required.");
        if (string.IsNullOrWhiteSpace(renderOutput))
            return Error.Validation(description: "render.output is required.");
        if (renderBody.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return Error.Validation(description: "render.body is required.");
        if (outputs.Count == 0)
            return Error.Validation(description: "At least one output is required.");

        string engine;
        try
        {
            engine = engineValidator.ValidateAndNormalize(renderEngine);
            outputValidator.ValidateOutputs(outputs);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation(description: ex.Message);
        }

        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (key is not null)
        {
            if (key.Length > 128)
                return Error.Validation(description: "Idempotency-Key must be at most 128 characters.");

            var existing = await db
                .Jobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.IdempotencyKey == key, cancellationToken);
            if (existing is not null)
                return new CreateJobResult { Job = existing, Created = false };
        }

        var now = DateTime.UtcNow;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            IdempotencyKey = key,
            Status = JobStatus.Queued,
            RenderEngine = engine,
            RenderInput = renderInput.Trim().ToLowerInvariant(),
            RenderOutput = renderOutput.Trim().ToLowerInvariant(),
            RenderBodyJson = renderBody.GetRawText(),
            OutputsJson = JobDeliverySerializer.Serialize(outputs),
            MaxAttempts = Math.Max(1, options.Value.DefaultMaxAttempts),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Jobs.Add(job);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (key is not null)
        {
            db.Entry(job).State = EntityState.Detached;
            var raced = await db
                .Jobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.IdempotencyKey == key, cancellationToken);
            if (raced is not null)
                return new CreateJobResult { Job = raced, Created = false };
            throw;
        }

        return new CreateJobResult { Job = job, Created = true };
    }

    public async Task<ErrorOr<Job>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        return job is null
            ? Error.NotFound(description: "Job not found.")
            : job;
    }
}

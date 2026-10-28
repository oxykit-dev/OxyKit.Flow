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
    public required IReadOnlyList<JobDelivery> Deliveries { get; init; }
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
            {
                var existingDeliveries = await LoadDeliveriesAsync(existing.Id, cancellationToken);
                return new CreateJobResult
                {
                    Job = existing,
                    Deliveries = existingDeliveries,
                    Created = false,
                };
            }
        }

        var now = DateTime.UtcNow;
        var jobId = Guid.NewGuid();
        var job = new Job
        {
            Id = jobId,
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

        var deliveryRows = new List<JobDelivery>(outputs.Count);
        for (var i = 0; i < outputs.Count; i++)
        {
            deliveryRows.Add(
                new JobDelivery
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    Ordinal = i,
                    Provider = outputs[i].Provider,
                    Destination = outputs[i].Destination,
                    Status = JobDeliveryStatus.Pending,
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
        }

        db.Jobs.Add(job);
        db.JobDeliveries.AddRange(deliveryRows);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (key is not null && ex.IsUniqueConstraintViolation())
        {
            db.Entry(job).State = EntityState.Detached;
            foreach (var row in deliveryRows)
                db.Entry(row).State = EntityState.Detached;

            var raced = await db
                .Jobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.IdempotencyKey == key, cancellationToken);
            if (raced is not null)
            {
                var racedDeliveries = await LoadDeliveriesAsync(raced.Id, cancellationToken);
                return new CreateJobResult
                {
                    Job = raced,
                    Deliveries = racedDeliveries,
                    Created = false,
                };
            }

            throw;
        }

        return new CreateJobResult
        {
            Job = job,
            Deliveries = deliveryRows,
            Created = true,
        };
    }

    public async Task<ErrorOr<(Job Job, IReadOnlyList<JobDelivery> Deliveries)>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
            return Error.NotFound(description: "Job not found.");

        var deliveries = await LoadDeliveriesAsync(id, cancellationToken);
        return (job, deliveries);
    }

    public const int DefaultListLimit = 50;
    public const int MaxListLimit = 100;

    public async Task<ErrorOr<ListJobsResult>> ListAsync(
        string? statusFilter,
        int? limit,
        CancellationToken cancellationToken = default
    )
    {
        JobStatus? status = null;
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            if (!Enum.TryParse<JobStatus>(statusFilter.Trim(), ignoreCase: true, out var parsed))
            {
                return Error.Validation(
                    description: $"Unknown status '{statusFilter}'. Use Queued, Processing, Completed, Failed, or DeadLettered."
                );
            }

            status = parsed;
        }

        var take = limit ?? DefaultListLimit;
        if (take < 1 || take > MaxListLimit)
            return Error.Validation(description: $"limit must be between 1 and {MaxListLimit}.");

        var query = db.Jobs.AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(j => j.Status == status);

        var total = await query.CountAsync(cancellationToken);
        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var ids = jobs.Select(j => j.Id).ToList();
        var allDeliveries = await db
            .JobDeliveries.AsNoTracking()
            .Where(d => ids.Contains(d.JobId))
            .OrderBy(d => d.Ordinal)
            .ToListAsync(cancellationToken);
        var byJob = allDeliveries.GroupBy(d => d.JobId).ToDictionary(g => g.Key, g => (IReadOnlyList<JobDelivery>)g.ToList());

        var items = jobs
            .Select(j =>
                (
                    Job: j,
                    Deliveries: byJob.TryGetValue(j.Id, out var d)
                        ? d
                        : Array.Empty<JobDelivery>()
                )
            )
            .ToList();

        return new ListJobsResult
        {
            Items = items,
            Count = items.Count,
            Total = total,
        };
    }

    public async Task<ErrorOr<(Job Job, IReadOnlyList<JobDelivery> Deliveries)>> RetryDeadLetteredAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
            return Error.NotFound(description: "Job not found.");

        if (job.Status != JobStatus.DeadLettered)
        {
            return Error.Validation(
                description: $"Only DeadLettered jobs can be retried (current status: {job.Status})."
            );
        }

        job.MarkAsRequeued();

        var deliveries = await db
            .JobDeliveries.Where(d => d.JobId == id)
            .OrderBy(d => d.Ordinal)
            .ToListAsync(cancellationToken);

        foreach (var delivery in deliveries)
        {
            if (delivery.Status != JobDeliveryStatus.Succeeded)
                delivery.MarkAsPendingForRetry();
        }

        await db.SaveChangesAsync(cancellationToken);
        return (job, deliveries);
    }

    private async Task<IReadOnlyList<JobDelivery>> LoadDeliveriesAsync(
        Guid jobId,
        CancellationToken cancellationToken
    ) =>
        await db
            .JobDeliveries.AsNoTracking()
            .Where(d => d.JobId == jobId)
            .OrderBy(d => d.Ordinal)
            .ToListAsync(cancellationToken);
}

public sealed class ListJobsResult
{
    public required IReadOnlyList<(Job Job, IReadOnlyList<JobDelivery> Deliveries)> Items { get; init; }
    public required int Count { get; init; }
    public required int Total { get; init; }
}

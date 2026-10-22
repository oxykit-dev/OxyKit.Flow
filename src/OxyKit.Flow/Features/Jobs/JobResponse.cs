using OxyKit.Flow.Infrastructure;
using OxyKit.Flow.Outputs;
using OxyKit.Flow.Services;

namespace OxyKit.Flow.Features.Jobs;

public sealed class JobResponse
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public string? IdempotencyKey { get; init; }
    public required string Status { get; init; }
    public required string RenderEngine { get; init; }
    public required string RenderInput { get; init; }
    public required string RenderOutput { get; init; }
    public required IReadOnlyList<DeliveryRequest> Outputs { get; init; }
    public required int AttemptCount { get; init; }
    public required int MaxAttempts { get; init; }
    public string? LastError { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    public static JobResponse From(Job job) =>
        new()
        {
            Id = job.Id,
            Name = job.Name,
            IdempotencyKey = job.IdempotencyKey,
            Status = job.Status.ToString(),
            RenderEngine = job.RenderEngine,
            RenderInput = job.RenderInput,
            RenderOutput = job.RenderOutput,
            Outputs = JobDeliverySerializer.Deserialize(job.OutputsJson),
            AttemptCount = job.AttemptCount,
            MaxAttempts = job.MaxAttempts,
            LastError = job.LastError,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            CompletedAt = job.CompletedAt,
        };
}

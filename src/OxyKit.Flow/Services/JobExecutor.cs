using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OxyKit.Flow.Abstractions;
using OxyKit.Flow.Infrastructure;
using OxyKit.Flow.Outputs;

namespace OxyKit.Flow.Services;

/// <summary>
/// Runs one persisted job: resolve render engine by name, then each pending output provider.
/// </summary>
public sealed class JobExecutor(
    FlowDbContext db,
    IEnumerable<IDocumentRenderer> engines,
    IEnumerable<IOutputProvider> outputs,
    ILogger<JobExecutor> logger
)
{
    public async Task ExecuteAsync(Job job, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Executing job {JobId} ({Name}): {Engine} {Input}/{Output}",
            job.Id,
            job.Name ?? "(unnamed)",
            job.RenderEngine,
            job.RenderInput,
            job.RenderOutput
        );

        var providers = outputs.ToList();
        var specs = JobDeliverySerializer.Deserialize(job.OutputsJson);
        var deliveries = await db
            .JobDeliveries.Where(d => d.JobId == job.Id)
            .OrderBy(d => d.Ordinal)
            .ToListAsync(cancellationToken);

        if (deliveries.Count == 0)
        {
            // Legacy jobs created before JobDelivery rows — seed from OutputsJson once.
            var now = DateTime.UtcNow;
            for (var i = 0; i < specs.Count; i++)
            {
                deliveries.Add(
                    new JobDelivery
                    {
                        Id = Guid.NewGuid(),
                        JobId = job.Id,
                        Ordinal = i,
                        Provider = specs[i].Provider,
                        Destination = specs[i].Destination,
                        Status = JobDeliveryStatus.Pending,
                        CreatedAt = now,
                        UpdatedAt = now,
                    }
                );
            }

            db.JobDeliveries.AddRange(deliveries);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (deliveries.TrueForAll(d => d.Status == JobDeliveryStatus.Succeeded))
        {
            logger.LogInformation(
                "Job {JobId} all deliveries already succeeded — skipping render",
                job.Id
            );
            return;
        }

        var engine =
            engines.FirstOrDefault(e =>
                string.Equals(e.Name, job.RenderEngine, StringComparison.OrdinalIgnoreCase)
            )
            ?? throw new InvalidOperationException(
                $"No render engine named '{job.RenderEngine}'."
            );

        using var bodyDoc = JsonDocument.Parse(job.RenderBodyJson);
        var artifact = await engine.RenderAsync(
            new RenderRequest
            {
                Input = job.RenderInput,
                Output = job.RenderOutput,
                Body = bodyDoc.RootElement.Clone(),
            },
            cancellationToken
        );

        foreach (var delivery in deliveries)
        {
            if (delivery.Status == JobDeliveryStatus.Succeeded)
            {
                logger.LogInformation(
                    "Job {JobId} skipping delivery {Ordinal} ({Provider}) — already succeeded",
                    job.Id,
                    delivery.Ordinal,
                    delivery.Provider
                );
                continue;
            }

            if (delivery.Ordinal < 0 || delivery.Ordinal >= specs.Count)
            {
                throw new InvalidOperationException(
                    $"Job {job.Id} delivery ordinal {delivery.Ordinal} is out of range for outputs."
                );
            }

            var request = specs[delivery.Ordinal];
            if (
                !string.Equals(
                    request.Provider,
                    delivery.Provider,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidOperationException(
                    $"Job {job.Id} delivery ordinal {delivery.Ordinal} provider '{delivery.Provider}' "
                        + $"does not match outputs JSON provider '{request.Provider}'."
                );
            }

            // OutputsJson is the send contract; JobDelivery is the checkpoint (must agree on provider).
            var provider = providers.FirstOrDefault(p =>
                string.Equals(p.Name, request.Provider, StringComparison.OrdinalIgnoreCase)
            );
            if (provider is null)
            {
                throw new InvalidOperationException(
                    $"No output provider named '{request.Provider}'."
                );
            }

            var context = new DeliveryContext { JobId = job.Id, Ordinal = delivery.Ordinal };

            try
            {
                var result = await provider.SendAsync(
                    artifact,
                    request,
                    context,
                    cancellationToken
                );
                delivery.MarkAsSucceeded(result.ExternalId);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Job {JobId} delivered via {Provider} → {Destination} (ordinal {Ordinal})",
                    job.Id,
                    request.Provider,
                    request.Destination,
                    delivery.Ordinal
                );
            }
            catch (Exception ex)
            {
                delivery.MarkAsFailed(ex.Message);
                await db.SaveChangesAsync(cancellationToken);
                throw;
            }
        }
    }
}

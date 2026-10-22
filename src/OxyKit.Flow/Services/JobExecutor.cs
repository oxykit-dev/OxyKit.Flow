using System.Text.Json;
using OxyKit.Flow.Abstractions;
using OxyKit.Flow.Infrastructure;
using OxyKit.Flow.Outputs;

namespace OxyKit.Flow.Services;

/// <summary>
/// Runs one persisted job: resolve render engine by name, then each output provider.
/// </summary>
public sealed class JobExecutor(
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

        var providers = outputs.ToList();
        var deliveries = JobDeliverySerializer.Deserialize(job.OutputsJson);

        foreach (var delivery in deliveries)
        {
            var provider = providers.FirstOrDefault(p =>
                string.Equals(p.Name, delivery.Provider, StringComparison.OrdinalIgnoreCase)
            );
            if (provider is null)
            {
                throw new InvalidOperationException(
                    $"No output provider named '{delivery.Provider}'."
                );
            }

            await provider.SendAsync(artifact, delivery, cancellationToken);
            logger.LogInformation(
                "Job {JobId} delivered via {Provider} → {Destination}",
                job.Id,
                delivery.Provider,
                delivery.Destination
            );
        }
    }
}

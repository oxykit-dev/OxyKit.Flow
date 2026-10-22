using System.Text.Json;
using OxyKit.Flow.Outputs;

namespace OxyKit.Flow.Features.Jobs;

public sealed class CreateJobRequest
{
    public string? Name { get; init; }

    public required RenderSpec Render { get; init; }

    public required List<DeliveryRequest> Outputs { get; init; }
}

public sealed class RenderSpec
{
    /// <summary>
    /// Registered render engine name. Defaults to <c>oxykit-render</c> when omitted.
    /// </summary>
    public string? Engine { get; init; }

    /// <summary>e.g. <c>table</c>, <c>template</c>, <c>html</c>, <c>csv</c>.</summary>
    public required string Input { get; init; }

    /// <summary>e.g. <c>pdf</c>, <c>html</c>, <c>xlsx</c>, <c>csv</c>.</summary>
    public required string Output { get; init; }

    /// <summary>Body matching the selected engine contract for that transform.</summary>
    public required JsonElement Body { get; init; }
}

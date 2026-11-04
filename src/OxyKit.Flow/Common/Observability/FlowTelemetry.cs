using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OxyKit.Flow.Common.Observability;

public static class FlowTelemetry
{
    public const string ActivitySourceName = "OxyKit.Flow";
    public const string MeterName = "OxyKit.Flow";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> JobsClaimed = Meter.CreateCounter<long>(
        "oxykit.flow.jobs.claimed"
    );
    public static readonly Counter<long> JobsSucceeded = Meter.CreateCounter<long>(
        "oxykit.flow.jobs.succeeded"
    );
    public static readonly Counter<long> JobsFailed = Meter.CreateCounter<long>(
        "oxykit.flow.jobs.failed"
    );
    public static readonly Histogram<double> JobDurationMs = Meter.CreateHistogram<double>(
        "oxykit.flow.jobs.duration",
        unit: "ms"
    );
}

using System.Net;
using FluentAssertions;

namespace OxyKit.Flow.Tests;

public sealed class OpenTelemetryConfigTests
{
    [Fact]
    public async Task Host_WithOpenTelemetryEnabled_AndBogusEndpoint_StillStarts()
    {
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:9");
        try
        {
            await using var factory = new FlowWebApplicationFactory().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("OpenTelemetry:Enabled", "true");
                builder.UseSetting("OpenTelemetry:ServiceName", "OxyKit.Flow.Tests");
            });
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
        }
    }
}

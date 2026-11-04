using System.Net;
using FluentAssertions;

namespace OxyKit.Flow.Tests;

public sealed class CorrelationIdTests : IClassFixture<FlowWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public CorrelationIdTests(FlowWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Health_WithoutCorrelationHeader_GeneratesAndEchoes()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Health_WithCorrelationHeader_EchoesSameValue()
    {
        const string expected = "flow-corr-test-001";
        _client.DefaultRequestHeaders.Add("X-Correlation-Id", expected);

        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Single().Should().Be(expected);
    }
}

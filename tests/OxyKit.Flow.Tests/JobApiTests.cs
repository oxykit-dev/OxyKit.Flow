using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace OxyKit.Flow.Tests;

public sealed class JobApiTests : IClassFixture<FlowWebApplicationFactory>, IDisposable
{
    private readonly FlowWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _anon;

    public JobApiTests(FlowWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-OxyKit-Flow-Key", "dev-flow-key");
        _anon = factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _anon.Dispose();
    }

    [Fact]
    public async Task Health_IsAnonymousOk()
    {
        var response = await _anon.GetAsync("/health", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task CreateJob_WithoutApiKey_IsUnauthorized()
    {
        var response = await _anon.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                name = "x",
                render = new
                {
                    input = "table",
                    output = "html",
                    body = new { sheets = Array.Empty<object>() },
                },
                outputs = new[] { new { provider = "file", destination = "output/x.html" } },
            },
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateJob_MissingOutputs_IsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                name = "bad",
                render = new
                {
                    input = "table",
                    output = "html",
                    body = new { },
                },
                outputs = Array.Empty<object>(),
            },
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAndGetJob_FileOutput_PersistsQueued()
    {
        var create = await _client.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                name = "unit-smoke",
                render = new
                {
                    input = "table",
                    output = "html",
                    body = new
                    {
                        sheets = new[]
                        {
                            new
                            {
                                name = "Orders",
                                headers = new[] { "Id", "Customer" },
                                rows = new object[] { new object[] { 1, "Acme" } },
                            },
                        },
                    },
                },
                outputs = new[]
                {
                    new { provider = "file", destination = "output/unit_smoke.html" },
                },
            },
            TestContext.Current.CancellationToken
        );
        create.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var id = created.GetProperty("id").GetGuid();

        var get = await _client.GetAsync($"/api/v1/jobs/{id}", TestContext.Current.CancellationToken);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var job = await get.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        job.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        job.GetProperty("renderEngine").GetString().Should().Be("oxykit-render");
        job.GetProperty("renderInput").GetString().Should().Be("table");
        job.GetProperty("renderOutput").GetString().Should().Be("html");
    }

    [Fact]
    public async Task CreateJob_ExplicitDefaultEngine_Persists()
    {
        var create = await _client.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                name = "engine-explicit",
                render = new
                {
                    engine = "oxykit-render",
                    input = "table",
                    output = "html",
                    body = new
                    {
                        sheets = new[]
                        {
                            new
                            {
                                name = "Orders",
                                headers = new[] { "Id" },
                                rows = new object[] { new object[] { 1 } },
                            },
                        },
                    },
                },
                outputs = new[]
                {
                    new { provider = "file", destination = "output/engine_explicit.html" },
                },
            },
            TestContext.Current.CancellationToken
        );
        create.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken
        );
        created.GetProperty("renderEngine").GetString().Should().Be("oxykit-render");
    }

    [Fact]
    public async Task CreateJob_UnknownEngine_IsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                name = "bad-engine",
                render = new
                {
                    engine = "crystal",
                    input = "table",
                    output = "html",
                    body = new { sheets = Array.Empty<object>() },
                },
                outputs = new[]
                {
                    new { provider = "file", destination = "output/bad.html" },
                },
            },
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}


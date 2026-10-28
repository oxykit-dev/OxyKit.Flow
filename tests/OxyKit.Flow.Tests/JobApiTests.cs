using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OxyKit.Flow.Infrastructure;

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
        var deliveries = job.GetProperty("deliveries");
        deliveries.GetArrayLength().Should().Be(1);
        deliveries[0].GetProperty("ordinal").GetInt32().Should().Be(0);
        deliveries[0].GetProperty("provider").GetString().Should().Be("file");
        deliveries[0].GetProperty("status").GetString().Should().Be("Pending");
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

    [Fact]
    public async Task ListJobs_WithoutApiKey_IsUnauthorized()
    {
        var response = await _anon.GetAsync("/api/v1/jobs/", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListJobs_ReturnsCreatedJob_AndFiltersByStatus()
    {
        var id = await CreateMinimalFileJobAsync("list-smoke");

        var list = await _client.GetAsync("/api/v1/jobs/?limit=20", TestContext.Current.CancellationToken);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("items")
            .EnumerateArray()
            .Any(j => j.GetProperty("id").GetGuid() == id)
            .Should()
            .BeTrue();

        var filtered = await _client.GetAsync(
            "/api/v1/jobs/?status=Queued&limit=50",
            TestContext.Current.CancellationToken
        );
        filtered.StatusCode.Should().Be(HttpStatusCode.OK);
        var filteredBody = await filtered.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken
        );
        foreach (var item in filteredBody.GetProperty("items").EnumerateArray())
            item.GetProperty("status").GetString().Should().Be("Queued");

        var bad = await _client.GetAsync(
            "/api/v1/jobs/?status=Nope",
            TestContext.Current.CancellationToken
        );
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var over = await _client.GetAsync(
            "/api/v1/jobs/?limit=101",
            TestContext.Current.CancellationToken
        );
        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RetryJob_DeadLettered_RequeuesWithFreshAttempts()
    {
        var id = await CreateMinimalFileJobAsync("retry-smoke");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FlowDbContext>();
            var job = await db.Jobs.FindAsync([id], TestContext.Current.CancellationToken);
            job.Should().NotBeNull();
            job!.AttemptCount = 3;
            job.MarkAsDeadLettered("unit test dead letter");
            var delivery = db.JobDeliveries.Single(d => d.JobId == id);
            delivery.MarkAsFailed("delivery failed");
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var retry = await _client.PostAsync(
            $"/api/v1/jobs/{id}/retry",
            null,
            TestContext.Current.CancellationToken
        );
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await retry.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("status").GetString().Should().Be("Queued");
        body.GetProperty("attemptCount").GetInt32().Should().Be(0);
        body.GetProperty("lastError").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("deliveries")[0].GetProperty("status").GetString().Should().Be("Pending");

        var again = await _client.PostAsync(
            $"/api/v1/jobs/{id}/retry",
            null,
            TestContext.Current.CancellationToken
        );
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RetryJob_NotFound_IsNotFound()
    {
        var response = await _client.PostAsync(
            $"/api/v1/jobs/{Guid.NewGuid()}/retry",
            null,
            TestContext.Current.CancellationToken
        );
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateMinimalFileJobAsync(string name)
    {
        var create = await _client.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                name,
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
                                headers = new[] { "Id" },
                                rows = new object[] { new object[] { 1 } },
                            },
                        },
                    },
                },
                outputs = new[]
                {
                    new { provider = "file", destination = $"output/{name}.html" },
                },
            },
            TestContext.Current.CancellationToken
        );
        create.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken
        );
        return created.GetProperty("id").GetGuid();
    }
}


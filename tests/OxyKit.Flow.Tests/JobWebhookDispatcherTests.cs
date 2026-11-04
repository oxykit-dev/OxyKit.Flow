using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxyKit.Flow.Infrastructure;
using OxyKit.Flow.Services;
using OxyKit.Observability;

namespace OxyKit.Flow.Tests;

public sealed class JobWebhookDispatcherTests
{
    [Fact]
    public void ValidateWebhookUrl_RejectsRelative()
    {
        JobWebhookDispatcher.ValidateWebhookUrl("/hooks").Should().NotBeNull();
        JobWebhookDispatcher.ValidateWebhookUrl("ftp://x").Should().NotBeNull();
        JobWebhookDispatcher.ValidateWebhookUrl("https://example.com/hook").Should().BeNull();
        JobWebhookDispatcher.ValidateWebhookUrl(null).Should().BeNull();
    }

    [Fact]
    public void ResolveUrl_PrefersJobOverDefault()
    {
        var job = SampleJob(webhookUrl: "https://job.example/hook");
        var opts = new WebhookOptions { DefaultUrl = "https://default.example/hook" };
        JobWebhookDispatcher.ResolveUrl(job, opts).Should().Be("https://job.example/hook");
    }

    [Fact]
    public void ResolveUrl_FallsBackToDefault()
    {
        var job = SampleJob(webhookUrl: null);
        var opts = new WebhookOptions { DefaultUrl = "https://default.example/hook" };
        JobWebhookDispatcher.ResolveUrl(job, opts).Should().Be("https://default.example/hook");
    }

    [Fact]
    public async Task NotifyTerminalAsync_PostsSignedPayload()
    {
        string? capturedBody = null;
        string? capturedSig = null;
        string? capturedCorrelation = null;

        var handler = new CapturingHandler(
            (req, body) =>
            {
                capturedBody = body;
                capturedSig = req.Headers.TryGetValues("X-OxyKit-Flow-Signature", out var sigs)
                    ? sigs.First()
                    : null;
                capturedCorrelation = req.Headers.TryGetValues("X-Correlation-Id", out var cids)
                    ? cids.First()
                    : null;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );

        var factory = new FakeHttpClientFactory(handler);
        var dispatcher = new JobWebhookDispatcher(
            factory,
            Options.Create(
                new WebhookOptions
                {
                    SigningSecret = "test-secret",
                    TimeoutSeconds = 5,
                }
            ),
            NullLogger<JobWebhookDispatcher>.Instance
        );

        CorrelationContext.Current = "corr-abc";
        try
        {
            var job = SampleJob(webhookUrl: "https://hooks.example/flow");
            job.MarkAsCompleted();
            await dispatcher.NotifyTerminalAsync(
                job,
                [
                    new JobDelivery
                    {
                        Id = Guid.NewGuid(),
                        JobId = job.Id,
                        Ordinal = 0,
                        Provider = "file",
                        Destination = "out.html",
                        Status = JobDeliveryStatus.Succeeded,
                        CreatedAt = job.CreatedAt,
                        UpdatedAt = job.UpdatedAt,
                        CompletedAt = job.CompletedAt,
                    },
                ],
                TestContext.Current.CancellationToken
            );
        }
        finally
        {
            CorrelationContext.Current = null;
        }

        capturedBody.Should().NotBeNullOrWhiteSpace();
        capturedBody!.Should().Contain("11111111-1111-1111-1111-111111111111");
        capturedBody.Should().Contain("\"status\":\"Completed\"");
        capturedCorrelation.Should().Be("corr-abc");

        var expected = Convert
            .ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes(capturedBody)))
            .ToLowerInvariant();
        capturedSig.Should().Be($"sha256={expected}");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task NotifyTerminalAsync_NoUrl_DoesNotPost()
    {
        var handler = new CapturingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var dispatcher = new JobWebhookDispatcher(
            new FakeHttpClientFactory(handler),
            Options.Create(new WebhookOptions()),
            NullLogger<JobWebhookDispatcher>.Instance
        );

        await dispatcher.NotifyTerminalAsync(
            SampleJob(webhookUrl: null),
            [],
            TestContext.Current.CancellationToken
        );
        handler.CallCount.Should().Be(0);
    }

    private static Job SampleJob(string? webhookUrl)
    {
        var now = DateTime.UtcNow;
        return new Job
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Status = JobStatus.Processing,
            RenderEngine = "oxykit-render",
            RenderInput = "table",
            RenderOutput = "html",
            RenderBodyJson = "{}",
            OutputsJson = """[{"provider":"file","destination":"out.html"}]""",
            WebhookUrl = webhookUrl,
            AttemptCount = 1,
            MaxAttempts = 3,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    private sealed class CapturingHandler(
        Func<HttpRequestMessage, string, HttpResponseMessage> onSend
    ) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            var body =
                request.Content is null
                    ? ""
                    : await request.Content.ReadAsStringAsync(cancellationToken);
            return onSend(request, body);
        }
    }
}

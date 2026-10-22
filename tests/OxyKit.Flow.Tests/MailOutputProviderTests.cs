using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxyKit.Flow.Outputs;

namespace OxyKit.Flow.Tests;

/// <summary>
/// Contract tests: Flow must treat Mail sent/accepted/scheduled as successful handoff
/// and must not re-send when Mail queues for its own retry.
/// </summary>
public sealed class MailOutputProviderTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, "sent")]
    [InlineData(HttpStatusCode.Accepted, "accepted")]
    [InlineData(HttpStatusCode.Accepted, "scheduled")]
    public async Task SendAsync_MailAcceptanceStatuses_Succeed(
        HttpStatusCode httpStatus,
        string status
    )
    {
        var handler = new CapturingHandler(
            httpStatus,
            $$"""{"emailId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","status":"{{status}}","message":"ok"}"""
        );
        var sut = CreateSut(handler);

        await sut.Invoking(s =>
                s.SendAsync(SampleArtifact(), SampleRequest(), TestContext.Current.CancellationToken)
            )
            .Should()
            .NotThrowAsync();

        handler.LastPath.Should().Be("api/v1/email/send");
        using var doc = JsonDocument.Parse(handler.LastBody!);
        var root = doc.RootElement;
        root.GetProperty("to").GetString().Should().Be("ops@example.com");
        root.GetProperty("subject").GetString().Should().Be("Invoice");
        root.GetProperty("attachments").GetArrayLength().Should().Be(1);
        root.GetProperty("attachments")[0]
            .GetProperty("fileName")
            .GetString()
            .Should()
            .Be("invoice.pdf");
    }

    [Fact]
    public async Task SendAsync_UsesDefaultSubjectTemplate_WhenSubjectOmitted()
    {
        var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"emailId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","status":"sent","message":"ok"}"""
        );
        var sut = CreateSut(handler);

        await sut.SendAsync(
            SampleArtifact(),
            new DeliveryRequest
            {
                Provider = "mail",
                Destination = "ops@example.com",
            },
            TestContext.Current.CancellationToken
        );

        using var doc = JsonDocument.Parse(handler.LastBody!);
        doc.RootElement.GetProperty("subject")
            .GetString()
            .Should()
            .Be("Report: invoice.pdf");
    }

    [Fact]
    public async Task SendAsync_UnknownStatus_Throws()
    {
        var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"emailId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","status":"queued","message":"nope"}"""
        );
        var sut = CreateSut(handler);

        await sut.Invoking(s =>
                s.SendAsync(SampleArtifact(), SampleRequest(), TestContext.Current.CancellationToken)
            )
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*emailId/status*");
    }

    [Fact]
    public async Task SendAsync_EmptyEmailId_Throws()
    {
        var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """{"emailId":"00000000-0000-0000-0000-000000000000","status":"sent","message":"ok"}"""
        );
        var sut = CreateSut(handler);

        await sut.Invoking(s =>
                s.SendAsync(SampleArtifact(), SampleRequest(), TestContext.Current.CancellationToken)
            )
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAsync_HttpFailure_Throws()
    {
        var handler = new CapturingHandler(
            HttpStatusCode.Unauthorized,
            """{"message":"missing key"}"""
        );
        var sut = CreateSut(handler);

        await sut.Invoking(s =>
                s.SendAsync(SampleArtifact(), SampleRequest(), TestContext.Current.CancellationToken)
            )
            .Should()
            .ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendAsync_MissingDestination_Throws()
    {
        var sut = CreateSut(
            new CapturingHandler(
                HttpStatusCode.OK,
                """{"emailId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","status":"sent","message":"ok"}"""
            )
        );

        await sut.Invoking(s =>
                s.SendAsync(
                    SampleArtifact(),
                    new DeliveryRequest { Provider = "mail", Destination = "  " },
                    TestContext.Current.CancellationToken
                )
            )
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("request");
    }

    [Fact]
    public async Task SendAsync_MissingApiKey_Throws()
    {
        var sut = CreateSut(
            new CapturingHandler(
                HttpStatusCode.OK,
                """{"emailId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","status":"sent","message":"ok"}"""
            ),
            apiKey: ""
        );

        await sut.Invoking(s =>
                s.SendAsync(SampleArtifact(), SampleRequest(), TestContext.Current.CancellationToken)
            )
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*ApiKey*");
    }

    private static MailOutputProvider CreateSut(CapturingHandler handler, string apiKey = "dev-key")
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5026/"),
        };
        return new MailOutputProvider(
            new FixedHttpClientFactory(http),
            Options.Create(
                new MailOutputOptions
                {
                    BaseAddress = new Uri("http://localhost:5026/"),
                    ApiKey = apiKey,
                }
            ),
            NullLogger<MailOutputProvider>.Instance
        );
    }

    private static Artifact SampleArtifact() =>
        new()
        {
            Content = Encoding.UTF8.GetBytes("%PDF-1.4"),
            ContentType = "application/pdf",
            FileName = "invoice.pdf",
        };

    private static DeliveryRequest SampleRequest() =>
        new()
        {
            Provider = "mail",
            Destination = "ops@example.com",
            Subject = "Invoice",
            Body = "Attached.",
        };

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastPath { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (request.RequestUri is not null)
                LastPath = request.RequestUri.PathAndQuery.TrimStart('/');

            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}

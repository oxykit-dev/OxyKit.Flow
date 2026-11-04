using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OxyKit.Flow.Features.Jobs;
using OxyKit.Flow.Infrastructure;
using OxyKit.Observability;

namespace OxyKit.Flow.Services;

public sealed class JobWebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    IOptions<WebhookOptions> options,
    ILogger<JobWebhookDispatcher> logger
) : IJobWebhookDispatcher
{
    public const string HttpClientName = "oxykit-flow-webhooks";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task NotifyTerminalAsync(
        Job job,
        IReadOnlyList<JobDelivery> deliveries,
        CancellationToken cancellationToken = default
    )
    {
        var url = ResolveUrl(job, options.Value);
        if (url is null)
            return;

        var payload = JobResponse.From(job, deliveries);
        var body = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

        var correlationId = CorrelationContext.Current;
        if (!string.IsNullOrWhiteSpace(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        var secret = options.Value.SigningSecret;
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
            var hex = Convert.ToHexString(hash).ToLowerInvariant();
            request.Headers.TryAddWithoutValidation("X-OxyKit-Flow-Signature", $"sha256={hex}");
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Webhook for job {JobId} returned {StatusCode} ({Url})",
                    job.Id,
                    (int)response.StatusCode,
                    url
                );
            }
            else
            {
                logger.LogInformation(
                    "Webhook for job {JobId} delivered ({Status})",
                    job.Id,
                    job.Status
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook for job {JobId} failed ({Url})", job.Id, url);
        }
    }

    public static string? ResolveUrl(Job job, WebhookOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(job.WebhookUrl))
            return job.WebhookUrl.Trim();
        if (!string.IsNullOrWhiteSpace(opts.DefaultUrl))
            return opts.DefaultUrl.Trim();
        return null;
    }

    /// <summary>Returns null when valid; otherwise a validation error description.</summary>
    public static string? ValidateWebhookUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        )
        {
            return "webhookUrl must be an absolute http or https URL.";
        }

        return null;
    }
}

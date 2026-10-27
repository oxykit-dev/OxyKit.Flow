using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace OxyKit.Flow.Outputs;

/// <summary>
/// Delivers an <see cref="Artifact"/> via OxyKit.Mail as an email attachment.
/// <see cref="DeliveryRequest.Destination"/> is the To address (comma-separated allowed).
/// Mail owns SMTP delivery; a 2xx with status sent/accepted/scheduled means the job
/// output succeeded and Flow must not re-send for SMTP flakiness.
/// Sends <c>Idempotency-Key</c> from <see cref="DeliveryContext"/> so Mail dedupes retries.
/// </summary>
public sealed class MailOutputProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<MailOutputOptions> options,
    ILogger<MailOutputProvider> logger
) : IOutputProvider
{
    public const string HttpClientName = "oxykit-mail";

    private static readonly HashSet<string> AcceptedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "sent",
        "accepted",
        "scheduled",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string Name => "mail";

    public async Task<DeliveryResult> SendAsync(
        Artifact artifact,
        DeliveryRequest request,
        DeliveryContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Destination))
            throw new ArgumentException("Mail destination (To) is required.", nameof(request));

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException(
                "OxyKit.Mail:ApiKey is required to use the mail output provider."
            );

        var fileName = string.IsNullOrWhiteSpace(artifact.FileName)
            ? GuessFileName(artifact.ContentType)
            : artifact.FileName!;

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? ApplyTemplate(settings.DefaultSubject, fileName)
            : request.Subject!;
        var body = string.IsNullOrWhiteSpace(request.Body)
            ? ApplyTemplate(settings.DefaultBody, fileName)
            : request.Body!;

        var payload = new MailSendRequest(
            To: request.Destination.Trim(),
            Cc: settings.DefaultCc ?? "",
            Bcc: settings.DefaultBcc ?? "",
            Subject: subject,
            Body: body,
            IsHtml: settings.DefaultIsHtml,
            Attachments:
            [
                new MailAttachmentRequest(fileName, artifact.Content, artifact.ContentType),
            ],
            ScheduledFor: null
        );

        var http = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/email/send")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", context.IdempotencyKey);

        using var response = await http.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "OxyKit.Mail send failed ({Status}): {Body}",
                (int)response.StatusCode,
                errorBody
            );
            response.EnsureSuccessStatusCode();
        }

        var acceptance = await response.Content.ReadFromJsonAsync<MailSendResponse>(
            JsonOptions,
            cancellationToken
        );

        if (
            acceptance is null
            || acceptance.EmailId == Guid.Empty
            || string.IsNullOrWhiteSpace(acceptance.Status)
            || !AcceptedStatuses.Contains(acceptance.Status)
        )
        {
            throw new InvalidOperationException(
                "OxyKit.Mail returned success without a valid emailId/status; refusing to treat as accepted."
            );
        }

        logger.LogInformation(
            "OxyKit.Mail accepted artifact {FileName} ({ContentType}) to {To}: emailId={EmailId} status={Status} http={HttpStatus} idempotencyKey={IdempotencyKey}",
            fileName,
            artifact.ContentType,
            request.Destination,
            acceptance.EmailId,
            acceptance.Status,
            (int)response.StatusCode,
            context.IdempotencyKey
        );

        return DeliveryResult.WithExternalId(acceptance.EmailId.ToString("D"));
    }

    private static string ApplyTemplate(string template, string fileName) =>
        template.Replace("{fileName}", fileName, StringComparison.OrdinalIgnoreCase);

    private static string GuessFileName(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "application/pdf" => "report.pdf",
            "text/html" => "report.html",
            "text/csv" => "report.csv",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "report.xlsx",
            _ => "report.bin",
        };

    private sealed record MailSendRequest(
        string To,
        string Cc,
        string Bcc,
        string Subject,
        string Body,
        bool IsHtml,
        List<MailAttachmentRequest>? Attachments,
        DateTime? ScheduledFor
    );

    private sealed record MailAttachmentRequest(
        string FileName,
        byte[] Content,
        string ContentType
    );

    private sealed record MailSendResponse(Guid EmailId, string Status, string? Message);
}

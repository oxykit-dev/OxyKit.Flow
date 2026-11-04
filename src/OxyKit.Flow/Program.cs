using FastEndpoints.Swagger;
using Microsoft.Extensions.Options;
using NSwag;
using OxyKit.Flow.Common;
using OxyKit.Flow.Common.Extensions;
using OxyKit.Flow.Common.Observability;
using OxyKit.Flow.Features.Jobs.Services;
using OxyKit.Flow.Outputs;
using OxyKit.Flow.Services;
using OxyKit.Observability;
using OxyKit.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddOxyKitJsonConsole();
builder.AddOxyKitOpenTelemetry(o =>
{
    o.ServiceName = "OxyKit.Flow";
    o.ActivitySources = [FlowTelemetry.ActivitySourceName];
    o.Meters = [FlowTelemetry.MeterName];
});
builder.Services.AddOxyKitFlowHost();
builder.Services.AddOxyKitFlowDatabase(builder.Configuration);
builder.Services.Configure<JobOptions>(builder.Configuration.GetSection(JobOptions.SectionName));
builder.Services.Configure<ArtifactCacheOptions>(
    builder.Configuration.GetSection(ArtifactCacheOptions.SectionName)
);
builder.Services.Configure<WebhookOptions>(
    builder.Configuration.GetSection(WebhookOptions.SectionName)
);
builder.Services.AddOptions<MailOutputOptions>().BindConfiguration(MailOutputOptions.SectionName);
builder
    .Services.AddOptions<TransferOutputOptions>()
    .BindConfiguration(TransferOutputOptions.SectionName);

builder.Services.AddDocumentRenderers(
    builder.Configuration,
    builder.Environment.ContentRootPath
);

builder.Services.AddTransient<CorrelationIdPropagatingHandler>();
builder
    .Services.AddHttpClient(
        MailOutputProvider.HttpClientName,
        (sp, http) =>
        {
            var mail = sp.GetRequiredService<IOptions<MailOutputOptions>>().Value;
            var baseAddress = mail.BaseAddress.ToString();
            if (!baseAddress.EndsWith('/'))
                baseAddress += "/";
            http.BaseAddress = new Uri(baseAddress);
            http.Timeout = TimeSpan.FromSeconds(mail.TimeoutSeconds <= 0 ? 120 : mail.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(mail.ApiKey))
                http.DefaultRequestHeaders.TryAddWithoutValidation(mail.ApiKeyHeader, mail.ApiKey);
        }
    )
    .AddHttpMessageHandler<CorrelationIdPropagatingHandler>();

builder.Services.AddSingleton<IOutputProvider, FileOutputProvider>();
builder.Services.AddSingleton<IOutputProvider, MailOutputProvider>();
builder.Services.AddSingleton<IOutputProvider, FtpOutputProvider>();
builder.Services.AddSingleton<IOutputProvider, SftpOutputProvider>();
builder.Services.AddSingleton<IArtifactCache, FileArtifactCache>();
builder.Services.AddSingleton<TransferOutputValidator>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<JobExecutor>();
builder.Services.AddScoped<IJobWebhookDispatcher, JobWebhookDispatcher>();
builder
    .Services.AddHttpClient(JobWebhookDispatcher.HttpClientName)
    .ConfigureHttpClient(
        (sp, http) =>
        {
            var timeout = sp.GetRequiredService<IOptions<WebhookOptions>>().Value.TimeoutSeconds;
            http.Timeout = TimeSpan.FromSeconds(timeout <= 0 ? 5 : timeout);
        }
    );
builder.Services.AddHostedService<JobQueueProcessor>();

builder.Services.SwaggerDocument(o =>
{
    o.ShortSchemaNames = true;
    o.DocumentSettings = s =>
    {
        s.DocumentName = "v1";
        s.Title = "OxyKit.Flow API";
        s.Version = "v1";
        s.AddAuth(
            AuthSchemeNames.ApiKey,
            new()
            {
                Name = "X-OxyKit-Flow-Key",
                In = OpenApiSecurityApiKeyLocation.Header,
                Type = OpenApiSecuritySchemeType.ApiKey,
            }
        );
    };
    o.ReleaseVersion = 1;
});

var app = builder.Build();

app.ApplyOxyKitFlowMigrations();
app.UseOxyKitFlowPipeline();
app.Run();

public partial class Program;

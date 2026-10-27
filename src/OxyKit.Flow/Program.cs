using FastEndpoints.Swagger;
using Microsoft.Extensions.Options;
using NSwag;
using OxyKit.Flow.Common;
using OxyKit.Flow.Common.Extensions;
using OxyKit.Flow.Features.Jobs.Services;
using OxyKit.Flow.Outputs;
using OxyKit.Flow.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOxyKitFlowHost();
builder.Services.AddOxyKitFlowDatabase(builder.Configuration);
builder.Services.Configure<JobOptions>(builder.Configuration.GetSection(JobOptions.SectionName));
builder.Services.AddOptions<MailOutputOptions>().BindConfiguration(MailOutputOptions.SectionName);
builder
    .Services.AddOptions<TransferOutputOptions>()
    .BindConfiguration(TransferOutputOptions.SectionName);

builder.Services.AddDocumentRenderers(
    builder.Configuration,
    builder.Environment.ContentRootPath
);

builder.Services.AddHttpClient(
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
);

builder.Services.AddSingleton<IOutputProvider, FileOutputProvider>();
builder.Services.AddSingleton<IOutputProvider, MailOutputProvider>();
builder.Services.AddSingleton<IOutputProvider, FtpOutputProvider>();
builder.Services.AddSingleton<IOutputProvider, SftpOutputProvider>();
builder.Services.AddSingleton<TransferOutputValidator>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<JobExecutor>();
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

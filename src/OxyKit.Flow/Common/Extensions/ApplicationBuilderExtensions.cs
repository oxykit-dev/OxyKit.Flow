using FastEndpoints;
using Scalar.AspNetCore;

namespace OxyKit.Flow.Common.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseOxyKitFlowPipeline(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpoints(c =>
        {
            c.Endpoints.RoutePrefix = "api";
            c.Versioning.Prefix = "v";
            c.Versioning.PrependToRoute = true;
        });

        if (app.Environment.IsDevelopment())
        {
            // Suite convention: /openapi/v1.json + /scalar
            app.UseOpenApi(c => c.Path = "/openapi/{documentName}.json");
            app.MapScalarApiReference();
        }

        // Outside FE versioning so paths stay /health and /v1/health (anonymous).
        app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
        app.MapGet("/v1/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
        return app;
    }
}

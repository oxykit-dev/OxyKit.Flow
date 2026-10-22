using Microsoft.Extensions.Configuration;
using OxyKit.Flow.Abstractions;
using OxyKit.Flow.Renderers;
using OxyKit.Render.Client;

namespace OxyKit.Flow.Common.Extensions;

public static class RenderEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDocumentRenderer"/> instances from <c>Render:Engines</c>.
    /// <c>Kind=oxykit</c> (default) uses the built-in Client adapter;
    /// <c>Kind=assembly</c> loads a third-party type — no host code changes required.
    /// </summary>
    public static IServiceCollection AddDocumentRenderers(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRoot = null
    )
    {
        services.Configure<RenderEngineOptions>(
            configuration.GetSection(RenderEngineOptions.SectionName)
        );

        var engines = ResolveEngineEndpoints(configuration);
        if (engines.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one render engine is required under Render:Engines "
                    + $"(or legacy OxyKit.Render for '{DocumentRenderers.DefaultEngineName}')."
            );
        }

        foreach (var (name, endpoint) in engines)
        {
            var engineName = name;
            var opts = endpoint;
            var kind = NormalizeKind(opts);

            if (kind.Equals(DocumentRenderers.KindOxyKit, StringComparison.OrdinalIgnoreCase))
            {
                services.AddSingleton<IDocumentRenderer>(_ =>
                    CreateOxyKitEngine(engineName, opts)
                );
            }
            else if (kind.Equals(DocumentRenderers.KindAssembly, StringComparison.OrdinalIgnoreCase))
            {
                services.AddSingleton<IDocumentRenderer>(sp =>
                    AssemblyDocumentRendererLoader.Create(sp, engineName, opts, contentRoot)
                );
            }
            else
            {
                throw new InvalidOperationException(
                    $"Render:Engines:{engineName}:Kind '{opts.Kind}' is not supported. "
                        + $"Use '{DocumentRenderers.KindOxyKit}' or '{DocumentRenderers.KindAssembly}'."
                );
            }
        }

        services.AddSingleton<RenderEngineValidator>();
        return services;
    }

    public static Dictionary<string, RenderEngineEndpointOptions> ResolveEngineEndpoints(
        IConfiguration configuration
    )
    {
        var engines = new Dictionary<string, RenderEngineEndpointOptions>(
            StringComparer.OrdinalIgnoreCase
        );

        var section = configuration.GetSection($"{RenderEngineOptions.SectionName}:Engines");
        foreach (var child in section.GetChildren())
        {
            var opts = new RenderEngineEndpointOptions
            {
                Kind = child["Kind"],
                BaseAddress = child["BaseAddress"],
                ApiKey = child["ApiKey"],
                Assembly = child["Assembly"],
                Type = child["Type"],
            };

            var kind = NormalizeKind(opts);
            if (kind.Equals(DocumentRenderers.KindOxyKit, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(opts.BaseAddress))
                    continue;
            }
            else if (kind.Equals(DocumentRenderers.KindAssembly, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(opts.Assembly) || string.IsNullOrWhiteSpace(opts.Type))
                    continue;
            }
            else
            {
                continue;
            }

            engines[child.Key] = opts;
        }

        if (!engines.ContainsKey(DocumentRenderers.DefaultEngineName))
        {
            var legacyBase = configuration["OxyKit.Render:BaseAddress"];
            if (!string.IsNullOrWhiteSpace(legacyBase))
            {
                engines[DocumentRenderers.DefaultEngineName] = new RenderEngineEndpointOptions
                {
                    Kind = DocumentRenderers.KindOxyKit,
                    BaseAddress = legacyBase,
                    ApiKey = configuration["OxyKit.Render:ApiKey"],
                };
            }
        }

        return engines;
    }

    public static string NormalizeKind(RenderEngineEndpointOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.Kind))
            return opts.Kind.Trim();

        if (!string.IsNullOrWhiteSpace(opts.Assembly) || !string.IsNullOrWhiteSpace(opts.Type))
            return DocumentRenderers.KindAssembly;

        return DocumentRenderers.KindOxyKit;
    }

    private static OxyKitRenderEngine CreateOxyKitEngine(
        string engineName,
        RenderEngineEndpointOptions opts
    )
    {
        if (string.IsNullOrWhiteSpace(opts.BaseAddress))
        {
            throw new InvalidOperationException(
                $"Render:Engines:{engineName}:BaseAddress is required when Kind=oxykit."
            );
        }

        var client = new RenderClient(
            new RenderClientOptions
            {
                BaseAddress = new Uri(opts.BaseAddress),
                ApiKey = opts.ApiKey,
            }
        );
        return new OxyKitRenderEngine(engineName, client);
    }
}

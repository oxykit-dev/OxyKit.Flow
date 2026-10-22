using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OxyKit.Flow.Abstractions;

namespace OxyKit.Flow.Renderers;

public static class AssemblyDocumentRendererLoader
{
    public static IDocumentRenderer Create(
        IServiceProvider services,
        string engineName,
        RenderEngineEndpointOptions options,
        string? contentRoot
    )
    {
        if (string.IsNullOrWhiteSpace(options.Assembly))
        {
            throw new InvalidOperationException(
                $"Render:Engines:{engineName}:Assembly is required when Kind=assembly."
            );
        }

        if (string.IsNullOrWhiteSpace(options.Type))
        {
            throw new InvalidOperationException(
                $"Render:Engines:{engineName}:Type is required when Kind=assembly."
            );
        }

        var path = ResolveAssemblyPath(options.Assembly, contentRoot);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Render engine '{engineName}' assembly not found: {path}",
                path
            );
        }

        var assembly = Assembly.LoadFrom(path);
        var type =
            assembly.GetType(options.Type, throwOnError: false, ignoreCase: false)
            ?? throw new InvalidOperationException(
                $"Type '{options.Type}' not found in assembly '{path}' for engine '{engineName}'."
            );

        if (!typeof(IDocumentRenderer).IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"Type '{options.Type}' must implement {nameof(IDocumentRenderer)}."
            );
        }

        var instance = (IDocumentRenderer)ActivatorUtilities.CreateInstance(services, type);
        if (!string.Equals(instance.Name, engineName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Render engine config key '{engineName}' must match IDocumentRenderer.Name '{instance.Name}'."
            );
        }

        return instance;
    }

    public static string ResolveAssemblyPath(string assembly, string? contentRoot)
    {
        if (Path.IsPathRooted(assembly))
            return Path.GetFullPath(assembly);

        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            var underContent = Path.GetFullPath(Path.Combine(contentRoot, assembly));
            if (File.Exists(underContent))
                return underContent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, assembly));
    }
}

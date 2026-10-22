using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OxyKit.Flow.Abstractions;
using OxyKit.Flow.Common.Extensions;
using OxyKit.Flow.Renderers;

namespace OxyKit.Flow.Tests;

public sealed class AssemblyDocumentRendererLoaderTests
{
    [Fact]
    public void ResolveAssemblyPath_Rooted_ReturnsFullPath()
    {
        var rooted = Path.Combine(Path.GetTempPath(), "plugin.dll");
        AssemblyDocumentRendererLoader
            .ResolveAssemblyPath(rooted, contentRoot: null)
            .Should()
            .Be(Path.GetFullPath(rooted));
    }

    [Fact]
    public async Task AddDocumentRenderers_LoadsAssemblyPlugin()
    {
        var assemblyPath = typeof(TestPluginDocumentRenderer).Assembly.Location;
        var typeName = typeof(TestPluginDocumentRenderer).FullName!;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Render:Engines:oxykit-render:BaseAddress"] = "http://localhost:5066/",
                    ["Render:Engines:test-plugin:Kind"] = "assembly",
                    ["Render:Engines:test-plugin:Assembly"] = assemblyPath,
                    ["Render:Engines:test-plugin:Type"] = typeName,
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddDocumentRenderers(config);

        await using var sp = services.BuildServiceProvider();
        var engines = sp.GetServices<IDocumentRenderer>().ToList();
        engines.Should().Contain(e => e.Name == "test-plugin");
        engines.Should().Contain(e => e.Name == DocumentRenderers.DefaultEngineName);

        var plugin = engines.Single(e => e.Name == "test-plugin");
        var artifact = await plugin.RenderAsync(
            new RenderRequest
            {
                Input = "x",
                Output = "y",
                Body = System.Text.Json.JsonDocument.Parse("""{"ok":true}""").RootElement.Clone(),
            },
            TestContext.Current.CancellationToken
        );

        artifact.FileName.Should().Be("plugin.json");
    }

    [Fact]
    public void Create_NameMismatch_Throws()
    {
        var assemblyPath = typeof(TestPluginDocumentRenderer).Assembly.Location;
        var typeName = typeof(TestPluginDocumentRenderer).FullName!;
        var services = new ServiceCollection().BuildServiceProvider();

        var act = () =>
            AssemblyDocumentRendererLoader.Create(
                services,
                engineName: "wrong-name",
                new RenderEngineEndpointOptions
                {
                    Kind = DocumentRenderers.KindAssembly,
                    Assembly = assemblyPath,
                    Type = typeName,
                },
                contentRoot: null
            );

        act.Should().Throw<InvalidOperationException>().WithMessage("*must match*");
    }
}

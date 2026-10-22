using FluentAssertions;
using Microsoft.Extensions.Configuration;
using OxyKit.Flow.Abstractions;
using OxyKit.Flow.Common.Extensions;
using OxyKit.Flow.Renderers;

namespace OxyKit.Flow.Tests;

public sealed class ResolveEngineEndpointsTests
{
    [Fact]
    public void Resolve_FromRenderEnginesSection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Render:Engines:oxykit-render:Kind"] = "oxykit",
                    ["Render:Engines:oxykit-render:BaseAddress"] = "http://localhost:5066/",
                    ["Render:Engines:oxykit-render:ApiKey"] = "dev-local-key",
                    ["Render:Engines:alt:BaseAddress"] = "http://localhost:5099/",
                }
            )
            .Build();

        var engines = RenderEngineServiceCollectionExtensions.ResolveEngineEndpoints(config);
        engines.Should().HaveCount(2);
        engines["oxykit-render"].BaseAddress.Should().Be("http://localhost:5066/");
        engines["alt"].BaseAddress.Should().Be("http://localhost:5099/");
        RenderEngineServiceCollectionExtensions
            .NormalizeKind(engines["alt"])
            .Should()
            .Be(DocumentRenderers.KindOxyKit);
    }

    [Fact]
    public void Resolve_AssemblyEngine_WithoutBaseAddress()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Render:Engines:oxykit-render:BaseAddress"] = "http://localhost:5066/",
                    ["Render:Engines:crystal:Kind"] = "assembly",
                    ["Render:Engines:crystal:Assembly"] = "plugins/Crystal.dll",
                    ["Render:Engines:crystal:Type"] = "MyCo.CrystalRenderer",
                }
            )
            .Build();

        var engines = RenderEngineServiceCollectionExtensions.ResolveEngineEndpoints(config);
        engines.Should().ContainKey("crystal");
        engines["crystal"].Assembly.Should().Be("plugins/Crystal.dll");
        RenderEngineServiceCollectionExtensions
            .NormalizeKind(engines["crystal"])
            .Should()
            .Be(DocumentRenderers.KindAssembly);
    }

    [Fact]
    public void Resolve_FallsBackToLegacyOxyKitRender()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OxyKit.Render:BaseAddress"] = "http://localhost:5066/",
                    ["OxyKit.Render:ApiKey"] = "legacy-key",
                }
            )
            .Build();

        var engines = RenderEngineServiceCollectionExtensions.ResolveEngineEndpoints(config);
        engines.Should().ContainKey(DocumentRenderers.DefaultEngineName);
        engines[DocumentRenderers.DefaultEngineName].ApiKey.Should().Be("legacy-key");
        engines[DocumentRenderers.DefaultEngineName].Kind.Should().Be(DocumentRenderers.KindOxyKit);
    }
}

using FluentAssertions;
using OxyKit.Flow.Abstractions;
using OxyKit.Flow.Renderers;

namespace OxyKit.Flow.Tests;

public sealed class RenderEngineValidatorTests
{
    private sealed class StubRenderer(string name) : IDocumentRenderer
    {
        public string Name { get; } = name;

        public Task<Artifact> RenderAsync(
            RenderRequest request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }

    [Fact]
    public void ValidateAndNormalize_OmitsEngine_UsesDefault()
    {
        var validator = new RenderEngineValidator(
            [new StubRenderer(DocumentRenderers.DefaultEngineName)]
        );

        validator.ValidateAndNormalize(null).Should().Be(DocumentRenderers.DefaultEngineName);
        validator.ValidateAndNormalize(" ").Should().Be(DocumentRenderers.DefaultEngineName);
    }

    [Fact]
    public void ValidateAndNormalize_Unknown_Throws()
    {
        var validator = new RenderEngineValidator(
            [new StubRenderer(DocumentRenderers.DefaultEngineName)]
        );

        var act = () => validator.ValidateAndNormalize("crystal");
        act.Should().Throw<ArgumentException>().WithMessage("*Unknown render engine*");
    }

    [Fact]
    public void ValidateAndNormalize_IsCaseInsensitive()
    {
        var validator = new RenderEngineValidator(
            [new StubRenderer(DocumentRenderers.DefaultEngineName)]
        );

        validator
            .ValidateAndNormalize("OxyKit-Render")
            .Should()
            .Be(DocumentRenderers.DefaultEngineName);
    }
}

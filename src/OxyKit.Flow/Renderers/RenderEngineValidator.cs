using OxyKit.Flow.Abstractions;

namespace OxyKit.Flow.Renderers;

/// <summary>
/// Fail-fast check that <c>render.engine</c> matches a registered <see cref="IDocumentRenderer"/>.
/// </summary>
public sealed class RenderEngineValidator(IEnumerable<IDocumentRenderer> engines)
{
    private readonly HashSet<string> _names = new(
        engines.Select(e => e.Name),
        StringComparer.OrdinalIgnoreCase
    );

    public IReadOnlyCollection<string> RegisteredNames => _names;

    /// <summary>
    /// Returns the normalized engine name, or throws <see cref="ArgumentException"/>.
    /// </summary>
    public string ValidateAndNormalize(string? engine)
    {
        var name = string.IsNullOrWhiteSpace(engine)
            ? DocumentRenderers.DefaultEngineName
            : engine.Trim();

        if (!_names.Contains(name))
        {
            throw new ArgumentException(
                $"Unknown render engine '{name}'. Registered: {string.Join(", ", _names.OrderBy(n => n))}."
            );
        }

        return _names.First(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }
}

using Microsoft.Extensions.Options;

namespace OxyKit.Flow.Outputs;

/// <summary>
/// Fail-fast checks for job outputs (known provider, transfer profile exists and matches protocol).
/// </summary>
public sealed class TransferOutputValidator(
    IEnumerable<IOutputProvider> providers,
    IOptions<TransferOutputOptions> transferOptions
)
{
    private readonly HashSet<string> _providerNames = new(
        providers.Select(p => p.Name),
        StringComparer.OrdinalIgnoreCase
    );

    public void ValidateOutputs(IReadOnlyList<DeliveryRequest> outputs)
    {
        foreach (var o in outputs)
        {
            if (string.IsNullOrWhiteSpace(o.Provider))
                throw new ArgumentException("Each output requires provider.");
            if (string.IsNullOrWhiteSpace(o.Destination))
                throw new ArgumentException("Each output requires destination.");

            var provider = o.Provider.Trim();
            if (!_providerNames.Contains(provider))
            {
                throw new ArgumentException(
                    $"Unknown output provider '{provider}'. Registered: {string.Join(", ", _providerNames.OrderBy(n => n))}."
                );
            }

            if (
                provider.Equals("ftp", StringComparison.OrdinalIgnoreCase)
                || provider.Equals("sftp", StringComparison.OrdinalIgnoreCase)
            )
            {
                var profile = TransferPathHelper.ResolveProfile(
                    transferOptions.Value,
                    o.Profile,
                    provider.ToLowerInvariant()
                );

                // Path shape only (artifact name filled at send time).
                _ = TransferPathHelper.ResolveRemotePath(
                    profile,
                    o.Destination,
                    artifactFileName: "placeholder.bin"
                );
            }
            else if (!string.IsNullOrWhiteSpace(o.Profile))
            {
                throw new ArgumentException(
                    $"Output provider '{provider}' does not use a transfer profile."
                );
            }
        }
    }
}

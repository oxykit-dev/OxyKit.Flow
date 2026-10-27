namespace OxyKit.Flow.Outputs;

public interface IOutputProvider
{
    string Name { get; }

    Task<DeliveryResult> SendAsync(
        Artifact artifact,
        DeliveryRequest request,
        DeliveryContext context,
        CancellationToken cancellationToken = default
    );
}

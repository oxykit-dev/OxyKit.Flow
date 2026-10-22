namespace OxyKit.Flow.Outputs;

public interface IOutputProvider
{
    string Name { get; }

    Task SendAsync(
        Artifact artifact,
        DeliveryRequest request,
        CancellationToken cancellationToken = default
    );
}

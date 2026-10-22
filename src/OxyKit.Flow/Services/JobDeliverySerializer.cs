using System.Text.Json;
using System.Text.Json.Serialization;
using OxyKit.Flow.Outputs;

namespace OxyKit.Flow.Services;

internal static class JobDeliverySerializer
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(IReadOnlyList<DeliveryRequest> outputs) =>
        JsonSerializer.Serialize(outputs, JsonOptions);

    public static IReadOnlyList<DeliveryRequest> Deserialize(string outputsJson)
    {
        var list = JsonSerializer.Deserialize<List<DeliveryRequest>>(outputsJson, JsonOptions);
        if (list is null || list.Count == 0)
            throw new InvalidOperationException("Job has no delivery outputs.");
        return list;
    }
}

namespace OxyKit.Flow.Services;

/// <summary>Optional Mail JWT under <c>Security:Jwt</c> (same Issuer/Audience/Key as Mail <c>Jwt:*</c>).</summary>
public sealed class JwtSecurityOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Key)
        && !string.IsNullOrWhiteSpace(Issuer)
        && !string.IsNullOrWhiteSpace(Audience);
}

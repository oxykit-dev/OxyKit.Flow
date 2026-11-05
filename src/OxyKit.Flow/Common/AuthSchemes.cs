namespace OxyKit.Flow.Common;

public static class AuthSchemeNames
{
    public const string ApiKey = "ApiKey";
    public const string Bearer = "Bearer";

    /// <summary>Policy scheme: API key header → ApiKey, else Bearer JWT.</summary>
    public const string ApiKeyOrBearer = "ApiKeyOrBearer";
}

public static class AuthPolicies
{
    /// <summary>Create / retry jobs — API key (Admin claim) or JWT Admin.</summary>
    public const string JobsWrite = "JobsWrite";

    /// <summary>List / get jobs — API key or JWT Admin|User.</summary>
    public const string JobsRead = "JobsRead";
}

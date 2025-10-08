namespace JUnitXmlImporter3.Options;

/// <summary>
/// HTTP-related configuration for AquaClient.
/// </summary>
public sealed class HttpOptions
{
    /// <summary>
    /// Overall HTTP timeout in seconds (default 100).
    /// </summary>
    public int TimeoutSeconds { get; init; } = 100;

    /// <summary>
    /// Maximum retry attempts for transient errors like 5xx/429 (default 3).
    /// </summary>
    public int Retries { get; init; } = 3;
}

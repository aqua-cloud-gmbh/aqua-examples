namespace JUnitXmlImporter3.Options;

/// <summary>
/// Controls run-level metadata applied to the import execution.
/// </summary>
public sealed class RunOptions
{
    /// <summary>
    /// Friendly name for this import run (e.g., pipeline/build identifier).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Optional external run identifier to correlate with CI systems.
    /// </summary>
    public string? ExternalRunId { get; init; }
}

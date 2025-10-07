namespace JUnitXmlImporter3.Options;

/// <summary>
/// Controls behavior when test cases cannot be mapped to Aqua IDs and related flow decisions.
/// </summary>
public sealed class BehaviorOptions
{
    public bool SkipUnmapped { get; init; } = true;
    public bool FailOnUnmapped { get; init; }
    /// <summary>
    /// When true, do not perform any network submission; log a summary of what would be sent.
    /// </summary>
    public bool DryRun { get; init; }
}

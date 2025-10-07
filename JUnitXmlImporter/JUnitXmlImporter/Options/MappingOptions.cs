namespace JUnitXmlImporter3.Options;

/// <summary>
/// Controls how test case IDs are resolved from test names.
/// </summary>
public sealed class MappingOptions
{
    /// <summary>
    /// Strategy name: "regex" (default) or "prefix-suffix".
    /// </summary>
    public string Strategy { get; init; } = "regex";

    /// <summary>
    /// Regex pattern used by RegexStrategy. Example: <![CDATA[(?:^|\b|\[)TC[:\-\s]?([0-9]{1,10})(?:\b|\])]]>
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Prefix used by PrefixSuffixStrategy (e.g., "TC").
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Number of digits expected after the prefix in PrefixSuffixStrategy.
    /// </summary>
    public int? DigitsLength { get; init; }
}

using System.Text.RegularExpressions;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Strict resolver for Test Scenario IDs from testcase names.
/// Matches TS followed by six or seven digits using a strict boundary-aware pattern.
/// Default pattern: <![CDATA[(?<![A-Z0-9])TS([0-9]{6,7})(?![0-9])]]>
/// Returns numeric value (leading zeros allowed in the matched text), constrained to [0..9_999_999].
/// </summary>
public sealed class RegexScenarioIdResolver(string? pattern = null) : IScenarioIdResolver
{
    private const string DefaultPattern = "(?<![A-Z0-9])TS([0-9]{6,7})(?![0-9])";
    private readonly Regex _regex = new(string.IsNullOrWhiteSpace(pattern) ? DefaultPattern : pattern,
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public int? ResolveId(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = _regex.Match(text);
        if (!match.Success || match.Groups.Count < 2) return null;
        var digits = match.Groups[1].Value; // six or seven digits, this may include leading zeros
        if (int.TryParse(digits, out var id) && id is >= 0 and <= 9_999_999) return id;
        return null;
    }
}

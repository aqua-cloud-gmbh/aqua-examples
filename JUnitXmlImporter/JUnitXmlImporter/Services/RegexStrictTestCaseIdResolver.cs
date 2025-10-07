using System.Text.RegularExpressions;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Strict resolver for Test Case IDs from testcase names.
/// Matches TC followed by exactly six digits using a strict boundary-aware pattern.
/// Default pattern: (?&lt;![A-Z0-9])TC([0-9]{6})(?![0-9])
/// Returns numeric value (leading zeros allowed in the matched text), constrained to [0..999 999].
/// </summary>
public sealed class RegexStrictTestCaseIdResolver(string? pattern = null) : ITestCaseIdResolver
{
    private const string DefaultPattern = "(?<![A-Z0-9])TC([0-9]{6})(?![0-9])";
    private readonly Regex _regex = new(string.IsNullOrWhiteSpace(pattern) ? DefaultPattern : pattern,
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public int? ResolveId(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = _regex.Match(text);
        if (!match.Success || match.Groups.Count < 2) return null;
        var digits = match.Groups[1].Value; // six digits, this may include leading zeros
        if (int.TryParse(digits, out var id) && id is >= 0 and <= 999_999) return id;
        return null;
    }
}

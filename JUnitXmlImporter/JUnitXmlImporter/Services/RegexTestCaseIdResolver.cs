using System.Text.RegularExpressions;
using JUnitXmlImporter3.Options;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Resolves test case IDs using a configurable regular expression pattern.
/// The first capturing group must contain the numeric ID.
/// Default pattern: <![CDATA[(?:^|\b|\[)TC[:\-\s]?([0-9]{1,10})(?:\b|\])]]>
/// </summary>
public sealed class RegexTestCaseIdResolver : ITestCaseIdResolver
{
    private static readonly string DefaultPattern = "(?:^|\\b|\\[)TC[:\\-\\s]?([0-9]{1,10})(?:\\b|\\])";
    private readonly Regex _regex;

    public RegexTestCaseIdResolver(MappingOptions? options)
    {
        options ??= new MappingOptions();
        var pattern = string.IsNullOrWhiteSpace(options.Pattern) ? DefaultPattern : options.Pattern!;
        _regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    public int? ResolveId(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = _regex.Match(text);
        if (!match.Success || match.Groups.Count < 2) return null;
        var value = match.Groups[1].Value;
        if (int.TryParse(value, out var id) && id > 0)
            return id;
        return null;
    }
}

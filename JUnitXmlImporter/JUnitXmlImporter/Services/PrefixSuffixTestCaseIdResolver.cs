using System.Globalization;
using JUnitXmlImporter3.Options;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Resolves IDs by finding a configured prefix (e.g., "TC") followed by optional separators and digits.
/// DigitsLength, when provided, enforces an exact length for the numeric part.
/// </summary>
public sealed class PrefixSuffixTestCaseIdResolver : ITestCaseIdResolver
{
    private readonly string _prefix;
    private readonly int? _digitsLength;

    public PrefixSuffixTestCaseIdResolver(MappingOptions? options)
    {
        options ??= new MappingOptions();
        _prefix = options.Prefix ?? "TC";
        _digitsLength = options.DigitsLength;
    }

    public int? ResolveId(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var index = IndexOfPrefix(text, _prefix);
        if (index < 0) return null;

        // Move past prefix and optional separators (:, -, space)
        var i = index + _prefix.Length;
        while (i < text.Length && (text[i] == ':' || text[i] == '-' || char.IsWhiteSpace(text[i]))) i++;

        // Collect digits
        var start = i;
        while (i < text.Length && char.IsDigit(text[i])) i++;
        var len = i - start;
        if (len <= 0) return null;

        if (_digitsLength.HasValue && len != _digitsLength.Value) return null;
        if (len > 10) return null; // guard against overflow by convention

        var span = text.AsSpan(start, len);
        if (int.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
        {
            return id;
        }
        return null;
    }

    private static int IndexOfPrefix(string text, string prefix)
    {
        // Case-insensitive search for prefix matching word boundary or start
        var comparison = StringComparison.OrdinalIgnoreCase;
        var idx = text.IndexOf(prefix, comparison);
        return idx;
    }
}

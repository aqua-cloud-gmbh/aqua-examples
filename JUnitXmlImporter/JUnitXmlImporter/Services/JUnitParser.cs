using System.Globalization;
using System.Text;
using System.Xml;
using JUnitXmlImporter3.Domain;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Streaming JUnit XML parser supporting common variants (Surefire, JUnit Platform).
/// Extracts classname, name, duration, outcome, error/skipped details, and timestamps when available.
/// </summary>
public sealed class JUnitParser : IJUnitParser
{
    public async Task<IReadOnlyList<TestCaseResult>> ParseAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        var settings = new XmlReaderSettings
        {
            Async = true,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Ignore
        };

        var results = new List<TestCaseResult>();
        DateTimeOffset? currentSuiteTimestamp = null;

        try
        {
            await using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, settings);

            while (await reader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                if (reader.Name is "testsuite" or "suite")
                {
                    // Capture timestamp if available
                    var ts = reader.GetAttribute("timestamp");
                    currentSuiteTimestamp = TryParseTimestamp(ts);
                }
                else if (reader.Name == "testcase")
                {
                    var result = await ReadTestCaseAsync(reader, currentSuiteTimestamp, cancellationToken);
                    if (result is not null)
                    {
                        results.Add(result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse JUnit XML at path: {path}", ex);
        }

        return results;
    }

    private static async Task<TestCaseResult?> ReadTestCaseAsync(XmlReader reader, DateTimeOffset? suiteTimestamp, CancellationToken cancellationToken)
    {
        // reader positioned at <testcase ...>
        var className = reader.GetAttribute("classname") ?? reader.GetAttribute("class") ?? string.Empty;
        var name = reader.GetAttribute("name") ?? string.Empty;
        var timeAttr = reader.GetAttribute("time");
        var durationSeconds = ParseDurationSeconds(timeAttr);

        TestOutcome outcome = TestOutcome.Passed;
        string? errMsg = null;
        string? errDetails = null;
        int? caseId = null;
        bool isEmptyElement = reader.IsEmptyElement;

        if (!isEmptyElement)
        {
            var depth = reader.Depth;
            while (await reader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth && reader.Name == "testcase")
                {
                    break;
                }

                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "failure":
                            outcome = TestOutcome.Failed;
                            (errMsg, errDetails) = await ReadIssueNodeAsync(reader, cancellationToken);
                            break;
                        case "error":
                            outcome = TestOutcome.Error;
                            (errMsg, errDetails) = await ReadIssueNodeAsync(reader, cancellationToken);
                            break;
                        case "skipped":
                        case "ignored":
                            outcome = TestOutcome.Skipped;
                            // Some junit-platform places a message attr on skipped
                            (errMsg, errDetails) = await ReadIssueNodeAsync(reader, cancellationToken, readInner:false);
                            break;
                        case "properties":
                            caseId = await ReadPropertiesForCaseIdAsync(reader, cancellationToken);
                            break;
                        default:
                            // skip others like system-out/system-err
                            if (reader.IsEmptyElement)
                                continue;
                            await reader.SkipAsync();
                            break;
                    }
                }
            }
        }

        DateTimeOffset? startedAt = suiteTimestamp;
        DateTimeOffset? finishedAt = null;
        if (suiteTimestamp.HasValue && durationSeconds.HasValue)
        {
            // Best-effort: assume cases run sequentially; we cannot be exact without per-case timestamps.
            // To avoid misleading data, only set FinishedAt when StartedAt known.
            finishedAt = suiteTimestamp.Value + TimeSpan.FromSeconds(durationSeconds.Value);
        }

        // Guard required fields per domain model
        if (string.IsNullOrWhiteSpace(name))
        {
            // Invalid testcase; skip
            return null;
        }

        return new TestCaseResult
        {
            ClassName = className,
            Name = name,
            DurationSeconds = durationSeconds,
            Outcome = outcome,
            ErrorMessage = errMsg,
            ErrorDetails = errDetails,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            CaseId = caseId
        };
    }

    private static async Task<(string? message, string? details)> ReadIssueNodeAsync(XmlReader reader, CancellationToken cancellationToken, bool readInner = true)
    {
        var message = reader.GetAttribute("message");
        if (reader.IsEmptyElement || !readInner)
        {
            // Advance if it is not empty to consume the element
            if (!reader.IsEmptyElement)
            {
                await reader.ReadAsync();
            }
            return (message, null);
        }

        var sb = new StringBuilder();
        var depth = reader.Depth;
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                break;
            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
            {
                sb.Append(reader.Value);
            }
        }
        return (message, sb.Length > 0 ? sb.ToString() : null);
    }

    private static double? ParseDurationSeconds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Try invariant first
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return seconds;
        // Fallback: replace comma decimal separators with dot
        var normalized = raw.Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            return seconds;
        // Last resort: try current culture
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds))
            return seconds;
        return null;
    }

    private static DateTimeOffset? TryParseTimestamp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return dto;
        if (DateTimeOffset.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dto))
            return dto;
        return null;
    }

    private static async Task<int?> ReadPropertiesForCaseIdAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        if (reader.IsEmptyElement)
            return null;

        var depth = reader.Depth;
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                break;

            if (reader.NodeType == XmlNodeType.Element && reader.Name == "property")
            {
                var name = reader.GetAttribute("name");
                var value = reader.GetAttribute("value");
                if (name == "case_id" && !string.IsNullOrWhiteSpace(value))
                {
                    if (int.TryParse(value, out var id))
                        return id;
                }
            }
        }
        return null;
    }
}

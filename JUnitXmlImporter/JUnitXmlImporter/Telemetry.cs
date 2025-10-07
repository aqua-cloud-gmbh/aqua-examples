using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JUnitXmlImporter3;

internal static class Telemetry
{
    public const string ServiceName = "JUnitXmlImporter";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName, version: null);

    // Metrics instruments (created once, reused)
    public static readonly Counter<long> FilesProcessed = Meter.CreateCounter<long>("importer.files_processed");
    public static readonly Counter<long> TestCasesParsed = Meter.CreateCounter<long>("importer.test_cases_parsed");
    public static readonly Counter<long> TestCasesMapped = Meter.CreateCounter<long>("importer.test_cases_mapped");
    public static readonly Counter<long> TestCasesSkippedUnmapped = Meter.CreateCounter<long>("importer.test_cases_skipped_unmapped");
    public static readonly Counter<long> ExecutionsPosted = Meter.CreateCounter<long>("aqua.executions_posted");
    public static readonly Counter<long> ExecutionsFailed = Meter.CreateCounter<long>("aqua.executions_failed");
}

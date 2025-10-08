using System.Diagnostics;
using JUnitXmlImporter3.Aqua;
using JUnitXmlImporter3.Domain;
using JUnitXmlImporter3.Options;
using Microsoft.Extensions.Logging;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Default importer pipeline implementation.
/// </summary>
public sealed class Importer(
    IInputDiscovery discovery,
    IJUnitParser parser,
    ITestCaseIdResolver idResolver,
    IScenarioIdResolver scenarioResolver,
    ImporterOptions options,
    ImporterInfrastructure infrastructure)
    : IImporter
{
    private readonly IInputDiscovery _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
    private readonly IJUnitParser _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    private readonly ITestCaseIdResolver _idResolver = idResolver ?? throw new ArgumentNullException(nameof(idResolver));
    private readonly IScenarioIdResolver _scenarioResolver = scenarioResolver ?? throw new ArgumentNullException(nameof(scenarioResolver));
    private readonly BehaviorOptions _behavior = (options ?? throw new ArgumentNullException(nameof(options))).Behavior;
    private readonly RunOptions _run = options.Run;
    private readonly AquaOptions _aqua = options.Aqua;
    private readonly ILogger<Importer> _logger = (infrastructure ?? throw new ArgumentNullException(nameof(infrastructure))).Logger;
    private readonly IClock _clock = infrastructure.Clock;
    private readonly IAquaClient _aquaClient = infrastructure.AquaClient;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        using var activity = Telemetry.ActivitySource.StartActivity("Importer.Run");
        var start = _clock.UtcNow;
        activity?.SetStartTime(start.UtcDateTime);

        var files = await DiscoverAndTagAsync(activity, cancellationToken);

        var agg = await AggregateAsync(files, cancellationToken);
        if (agg.ExitCode is { } earlyExit)
        {
            return earlyExit;
        }

        if (_behavior.DryRun)
        {
            return HandleDryRun(agg.ScenarioToExecutions, agg.FilesProcessed, agg.TestCasesParsed, agg.Mapped, agg.SkippedUnmapped);
        }

        var (posted, failedPosts) = await SubmitScenarioGroupsAsync(agg.ScenarioToExecutions, cancellationToken);

        _logger.LogInformation("Summary: {@Summary}",
            new
            {
                files = agg.FilesProcessed,
                parsed = agg.TestCasesParsed,
                mapped = agg.Mapped,
                skippedUnmapped = agg.SkippedUnmapped,
                posted = posted,
                failed = failedPosts
            });

        if (failedPosts > 0)
        {
            return 3;
        }

        return GetExitCodeForSkippedUnmapped(agg.SkippedUnmapped);
    }

    private async Task<IReadOnlyList<string>> DiscoverAndTagAsync(Activity? activity, CancellationToken cancellationToken)
    {
        var files = await _discovery.DiscoverAsync(cancellationToken);
        activity?.SetTag("importer.files_discovered", files.Count);
        activity?.SetTag("run.name", _run.Name);
        if (!string.IsNullOrEmpty(_run.ExternalRunId)) activity?.SetTag("run.external_id", _run.ExternalRunId);
        if (_aqua.ProjectId is { } pid) activity?.SetTag("aqua.project_id", pid);
        return files;
    }

    private sealed record AggregationResult(
        int FilesProcessed,
        int TestCasesParsed,
        int Mapped,
        int SkippedUnmapped,
        Dictionary<int, List<AquaExecutionRequest>> ScenarioToExecutions,
        int? ExitCode);

    private async Task<AggregationResult> AggregateAsync(IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        int filesProcessed = 0;
        int testCasesParsed = 0;
        int mapped = 0;
        int skippedUnmapped = 0;
        int? exitCode = null;
        var scenarioToExecutions = new Dictionary<int, List<AquaExecutionRequest>>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            filesProcessed++;
            Telemetry.FilesProcessed.Add(1);

            var fileAgg = await AggregateFileAsync(
                file,
                scenarioToExecutions,
                cancellationToken);

            testCasesParsed += fileAgg.TestCasesParsedDelta;
            mapped += fileAgg.MappedDelta;
            skippedUnmapped += fileAgg.SkippedUnmappedDelta;

            if (fileAgg.ExitCode is { } code)
            {
                exitCode = code;
                break;
            }
        }

        return new AggregationResult(filesProcessed, testCasesParsed, mapped, skippedUnmapped, scenarioToExecutions, exitCode);
    }

    private async Task<(int TestCasesParsedDelta, int MappedDelta, int SkippedUnmappedDelta, int? ExitCode)> AggregateFileAsync(
        string file,
        Dictionary<int, List<AquaExecutionRequest>> scenarioToExecutions,
        CancellationToken cancellationToken)
    {
        var parseResult = await TryParseFileAsync(file, cancellationToken);
        if (parseResult.ExitCode is { } parseExit)
        {
            return (0, 0, 0, parseExit);
        }

        var results = parseResult.Results!;
        var parsedDelta = results.Count;
        Telemetry.TestCasesParsed.Add(results.Count);

        int mappedDelta = 0;
        int skippedDelta = 0;

        foreach (var r in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (exec, resultExitCode) = ProcessResult(r, ref mappedDelta, ref skippedDelta);
            if (resultExitCode is { } code)
            {
                return (parsedDelta, mappedDelta, skippedDelta, code);
            }

            if (exec is null)
            {
                continue;
            }

            AddExecutionToScenarioGroup(exec, scenarioToExecutions);
        }

        return (parsedDelta, mappedDelta, skippedDelta, null);
    }

    private static void AddExecutionToScenarioGroup(
        AquaExecutionRequest exec,
        Dictionary<int, List<AquaExecutionRequest>> scenarioToExecutions)
    {
        if (!scenarioToExecutions.TryGetValue(exec.ScenarioId, out var list))
        {
            list = new List<AquaExecutionRequest>();
            scenarioToExecutions[exec.ScenarioId] = list;
        }

        list.Add(exec);
    }

    private async Task<(IReadOnlyList<TestCaseResult>? Results, int? ExitCode)> TryParseFileAsync(string file, CancellationToken cancellationToken)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        using var parseActivity = Telemetry.ActivitySource.StartActivity("Importer.ParseFile");
        parseActivity?.SetTag("file.path", file);
        try
        {
            var results = await _parser.ParseAsync(file, cancellationToken);
            parseActivity?.SetTag("testcases.count", results.Count);
            return (results, null);
        }
        catch (Exception ex)
        {
            parseActivity?.AddException(ex);
            parseActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Error parsing file: {File}", file);
            return (null, 2);
        }
    }

    private (AquaExecutionRequest? Exec, int? ExitCode) ProcessResult(TestCaseResult r, ref int mapped, ref int skippedUnmapped)
    {
        var nameContext = r.Name;
        var altContext = string.IsNullOrWhiteSpace(r.ClassName) ? r.Name : $"{r.ClassName}.{r.Name}";
        var scenarioId = _scenarioResolver.ResolveId(nameContext) ?? _scenarioResolver.ResolveId(altContext);
        var caseId = _idResolver.ResolveId(nameContext) ?? _idResolver.ResolveId(altContext);

        if (scenarioId is null || scenarioId < 0 || caseId is null || caseId <= 0)
        {
            if (_behavior.SkipUnmapped)
            {
                skippedUnmapped++;
                Telemetry.TestCasesSkippedUnmapped.Add(1);
                _logger.LogWarning("Unmapped IDs skipped: {Class}.{Name} (ScenarioId: {ScenarioId}, CaseId: {CaseId})", r.ClassName, r.Name, scenarioId, caseId);
                return (null, null);
            }

            if (_behavior.FailOnUnmapped)
            {
                _logger.LogError("Unmapped IDs and fail-on-unmapped enabled: {Class}.{Name} (ScenarioId: {ScenarioId}, CaseId: {CaseId})", r.ClassName, r.Name, scenarioId, caseId);
                return (null, 2);
            }

            skippedUnmapped++;
            _logger.LogWarning("Unmapped IDs treated as skipped: {Class}.{Name} (ScenarioId: {ScenarioId}, CaseId: {CaseId})", r.ClassName, r.Name, scenarioId, caseId);
            return (null, null);
        }

        mapped++;
        Telemetry.TestCasesMapped.Add(1);
        var durationMs = r.DurationSeconds.HasValue ? (int?)Math.Round(r.DurationSeconds.Value * 1000, MidpointRounding.AwayFromZero) : null;
        var exec = new AquaExecutionRequest
        {
            TestCaseId = caseId.Value,
            Status = MapStatus(r.Outcome),
            DurationMs = durationMs,
            StartedAt = r.StartedAt,
            FinishedAt = r.FinishedAt,
            ErrorMessage = r.ErrorMessage,
            ErrorDetails = r.ErrorDetails,
            ExternalRunId = _run.ExternalRunId,
            RunName = _run.Name,
            ProjectId = _aqua.ProjectId,
            ScenarioId = scenarioId.Value
        };

        return (exec, null);
    }

    private int HandleDryRun(Dictionary<int, List<AquaExecutionRequest>> scenarioToExecutions, int filesProcessed, int testCasesParsed, int mapped, int skippedUnmapped)
    {
        var totalPlanned = scenarioToExecutions.Values.Sum(l => l.Count);
        _logger.LogInformation("Dry-run enabled: would submit {Groups} scenario group(s), {TotalPlanned} execution(s). No network calls performed.",
            scenarioToExecutions.Count, totalPlanned);
        _logger.LogInformation("Summary: files={Files}, parsed={Parsed}, mapped={Mapped}, skipped-unmapped={Skipped}, posted={Posted}, failed={Failed}",
            filesProcessed, testCasesParsed, mapped, skippedUnmapped, 0, 0);
        return GetExitCodeForSkippedUnmapped(skippedUnmapped);
    }

    private async Task<(int Posted, int FailedPosts)> SubmitScenarioGroupsAsync(Dictionary<int, List<AquaExecutionRequest>> scenarioToExecutions, CancellationToken cancellationToken)
    {
        int posted = 0;
        int failedPosts = 0;

        foreach (var kvp in scenarioToExecutions)
        {
            var groupScenarioId = kvp.Key;
            var group = (IReadOnlyList<AquaExecutionRequest>)kvp.Value;

            // ReSharper disable once ExplicitCallerInfoArgument
            using var submitActivity = Telemetry.ActivitySource.StartActivity("Importer.SubmitScenarioGroup");
            submitActivity?.SetTag("aqua.scenario_id", groupScenarioId);
            submitActivity?.SetTag("executions.count", group.Count);

            var result = await _aquaClient.SubmitExecutionsAsync(group, cancellationToken);
            posted += result.Posted;
            failedPosts += result.Failed;
            Telemetry.ExecutionsPosted.Add(result.Posted);
            Telemetry.ExecutionsFailed.Add(result.Failed);

            if (!result.Success)
            {
                submitActivity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage ?? "failed");
                _logger.LogError("Submission failed for scenario {ScenarioId}", groupScenarioId);
            }
        }

        return (posted, failedPosts);
    }

    private static string MapStatus(TestOutcome outcome)
    {
        // Centralized mapping
        return outcome switch
        {
            TestOutcome.Passed => "Pass",
            TestOutcome.Failed => "Failed",
            TestOutcome.Error => "Incomplete",
            TestOutcome.Skipped => "NotRun",
            _ => "Passed"
        };
    }

    private static int GetExitCodeForSkippedUnmapped(int skippedUnmapped)
    {
        return skippedUnmapped > 0 ? 2 : 0;
    }
}

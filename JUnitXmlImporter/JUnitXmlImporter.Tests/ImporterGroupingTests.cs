using JUnitXmlImporter3.Aqua;
using JUnitXmlImporter3.Domain;
using JUnitXmlImporter3.Options;
using JUnitXmlImporter3.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class ImporterGroupingTests
{
    [Test]
    public async Task RunAsync_GroupsByScenario_SubmitsOncePerScenario()
    {
        var discovery = new SingleFileDiscovery();
        var parser = new FakeParser([
            new TestCaseResult { ClassName = "A", Name = "name1", Outcome = TestOutcome.Passed },
            new TestCaseResult { ClassName = "B", Name = "name2", Outcome = TestOutcome.Failed }
        ]);
        var idResolver = new CountingCaseResolver();
        var scenarioResolver = new FixedScenarioResolver(10, 11);
        var behavior = new BehaviorOptions { DryRun = false };
        var run = new RunOptions();
        var aqua = new AquaOptions { BaseUrl = "https://example.com", Username = "u", Password = "p" };
        var clock = new SystemClock();
        var client = new RecordingClient();
        var options = new ImporterOptions(behavior, run, aqua);
        var infrastructure = new ImporterInfrastructure(clock, client, NullLogger<Importer>.Instance);
        var sut = new Importer(discovery, parser, idResolver, scenarioResolver, options, infrastructure);

        var code = await sut.RunAsync(CancellationToken.None);

        code.ShouldBe(0);
        client.Calls.ShouldBe(2); // one per scenario
        client.TotalExecutions.ShouldBe(2);
    }

    private sealed class SingleFileDiscovery : IInputDiscovery
    {
        public Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken) => Task.FromResult((IReadOnlyList<string>)
            ["file.xml"]);
    }
    private sealed class FakeParser(IReadOnlyList<TestCaseResult> results) : IJUnitParser
    {
        public Task<IReadOnlyList<TestCaseResult>> ParseAsync(string path, CancellationToken cancellationToken) => Task.FromResult(results);
    }
    private sealed class CountingCaseResolver : ITestCaseIdResolver
    {
        private int _current = 40;
        public int? ResolveId(string? text) => Interlocked.Increment(ref _current);
    }
    private sealed class FixedScenarioResolver(params int[] ids) : IScenarioIdResolver
    {
        private int _i;
        public int? ResolveId(string? text) => ids[_i++ % ids.Length];
    }
    private sealed class RecordingClient : IAquaClient
    {
        public int Calls { get; private set; }
        public int TotalExecutions { get; private set; }
        public Task<AquaSubmitResult> SubmitExecutionsAsync(IReadOnlyList<AquaExecutionRequest> executions, CancellationToken cancellationToken)
        {
            Calls++;
            TotalExecutions += executions.Count;
            return Task.FromResult(new AquaSubmitResult { Success = true, Posted = executions.Count, Failed = 0 });
        }
    }
}

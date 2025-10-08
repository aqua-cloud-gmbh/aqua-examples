using JUnitXmlImporter3.Aqua;
using JUnitXmlImporter3.Domain;
using JUnitXmlImporter3.Options;
using JUnitXmlImporter3.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class ImporterBehaviorTests
{
    [Test]
    public async Task RunAsync_SkipUnmapped_Returns2_NoApiCalls()
    {
        var files = new[] { "file1.xml" };
        var discovery = new FakeDiscovery(files);
        var parser = new FakeParser([new TestCaseResult { ClassName = "C", Name = "no ids" }]);
        var idResolver = new AlwaysNullCaseResolver();
        var scenarioResolver = new AlwaysNullScenarioResolver();
        var behavior = new BehaviorOptions { SkipUnmapped = true, FailOnUnmapped = false, DryRun = true };
        var run = new RunOptions();
        var aqua = new AquaOptions();
        var clock = new SystemClock();
        var client = new RecordingAquaClient();
        var options = new ImporterOptions(behavior, run, aqua);
        var infrastructure = new ImporterInfrastructure(clock, client, NullLogger<Importer>.Instance);
        var sut = new Importer(discovery, parser, idResolver, scenarioResolver, options, infrastructure);

        var code = await sut.RunAsync(CancellationToken.None);

        code.ShouldBe(2);
        client.Calls.ShouldBe(0);
    }

    [Test]
    public async Task RunAsync_FailOnUnmapped_Returns2_Immediately()
    {
        var files = new[] { "file1.xml" };
        var discovery = new FakeDiscovery(files);
        var parser = new FakeParser([new TestCaseResult { ClassName = "C", Name = "no ids" }]);
        var idResolver = new AlwaysNullCaseResolver();
        var scenarioResolver = new AlwaysNullScenarioResolver();
        var behavior = new BehaviorOptions { SkipUnmapped = false, FailOnUnmapped = true, DryRun = false };
        var run = new RunOptions();
        var aqua = new AquaOptions();
        var clock = new SystemClock();
        var client = new RecordingAquaClient();
        var options = new ImporterOptions(behavior, run, aqua);
        var infrastructure = new ImporterInfrastructure(clock, client, NullLogger<Importer>.Instance);
        var sut = new Importer(discovery, parser, idResolver, scenarioResolver, options, infrastructure);

        var code = await sut.RunAsync(CancellationToken.None);

        code.ShouldBe(2);
        client.Calls.ShouldBe(0);
    }

    private sealed class FakeDiscovery(IReadOnlyList<string> files) : IInputDiscovery
    {
        public Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken) => Task.FromResult(files);
    }

    private sealed class FakeParser(IReadOnlyList<TestCaseResult> results) : IJUnitParser
    {
        public Task<IReadOnlyList<TestCaseResult>> ParseAsync(string path, CancellationToken cancellationToken) => Task.FromResult(results);
    }

    private sealed class AlwaysNullCaseResolver : ITestCaseIdResolver { public int? ResolveId(string? text) => null; }
    private sealed class AlwaysNullScenarioResolver : IScenarioIdResolver { public int? ResolveId(string? text) => null; }

    private sealed class RecordingAquaClient : IAquaClient
    {
        public int Calls { get; private set; }
        public Task<AquaSubmitResult> SubmitExecutionsAsync(IReadOnlyList<AquaExecutionRequest> executions, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AquaSubmitResult { Success = true, Posted = executions.Count, Failed = 0 });
        }
    }
}

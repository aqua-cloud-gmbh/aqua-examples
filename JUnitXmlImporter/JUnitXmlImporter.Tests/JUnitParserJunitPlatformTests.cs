using System.Globalization;
using JUnitXmlImporter3.Domain;
using JUnitXmlImporter3.Services;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class JUnitParserJunitPlatformTests
{
    [Test]
    public async Task ParseAsync_JunitPlatformSample_ParsesParameterizedAndSkipped()
    {
        // Arrange
        var parser = new JUnitParser();
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "junit-platform-sample.xml");

        // Act
        var results = await parser.ParseAsync(path, CancellationToken.None);

        // Assert
        results.Count.ShouldBe(3);
        var suiteTs = DateTimeOffset.Parse("2025-08-21T09:00:00Z", CultureInfo.InvariantCulture);

        var r1 = results[0];
        r1.ClassName.ShouldBe("com.example.ParamTests");
        r1.Name.ShouldBe("shouldWork(String)[1] - input=foo");
        r1.Outcome.ShouldBe(TestOutcome.Passed);
        r1.DurationSeconds!.Value.ShouldBe(0.100, 0.000_001);
        r1.ErrorMessage.ShouldBeNull();
        r1.ErrorDetails.ShouldBeNull();
        r1.StartedAt.ShouldBe(suiteTs);
        r1.FinishedAt.ShouldBe(suiteTs + TimeSpan.FromSeconds(0.100));

        var r2 = results[1];
        r2.ClassName.ShouldBe("com.example.ParamTests");
        r2.Name.ShouldBe("shouldWork(String)[2] - input=bar");
        r2.Outcome.ShouldBe(TestOutcome.Failed);
        r2.DurationSeconds!.Value.ShouldBe(0.050, 0.000_001);
        r2.ErrorMessage.ShouldBe("expected:<42> but was:<41>");
        r2.ErrorDetails.ShouldNotBeNull();
        r2.ErrorDetails!.ShouldContain("java.lang.AssertionError");
        r2.StartedAt.ShouldBe(suiteTs);
        r2.FinishedAt.ShouldBe(suiteTs + TimeSpan.FromSeconds(0.050));

        var r3 = results[2];
        r3.ClassName.ShouldBe("com.example.SkippedTests");
        r3.Name.ShouldBe("shouldBeSkipped()");
        r3.Outcome.ShouldBe(TestOutcome.Skipped);
        r3.DurationSeconds!.Value.ShouldBe(0.0, 0.000_001);
        r3.ErrorMessage.ShouldBe("@Disabled reason");
        r3.ErrorDetails.ShouldBeNull();
        r3.StartedAt.ShouldBe(suiteTs);
        r3.FinishedAt.ShouldBe(suiteTs + TimeSpan.FromSeconds(0.0));
    }
}

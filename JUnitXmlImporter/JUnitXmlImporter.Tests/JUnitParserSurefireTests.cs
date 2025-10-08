using System.Globalization;
using JUnitXmlImporter3.Domain;
using JUnitXmlImporter3.Services;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class JUnitParserSurefireTests
{
    [Test]
    public async Task ParseAsync_SurefireSample_ParsesAllFields()
    {
        // Arrange
        var parser = new JUnitParser();
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "surefire-sample.xml");

        // Act
        var results = await parser.ParseAsync(path, CancellationToken.None);

        // Assert
        results.Count.ShouldBe(4);

        var suiteTs = DateTimeOffset.Parse("2025-08-20T12:34:56Z", CultureInfo.InvariantCulture);

        var r1 = results[0];
        r1.ClassName.ShouldBe("WebTests.LoginTests");
        r1.Name.ShouldBe("Should login [TC:123]");
        r1.Outcome.ShouldBe(TestOutcome.Passed);
        r1.DurationSeconds.ShouldNotBeNull();
        r1.DurationSeconds!.Value.ShouldBe(0.123, 0.000_001);
        r1.ErrorMessage.ShouldBeNull();
        r1.ErrorDetails.ShouldBeNull();
        r1.StartedAt.ShouldBe(suiteTs);
        r1.FinishedAt.ShouldBe(suiteTs + TimeSpan.FromSeconds(0.123));

        var r2 = results[1];
        r2.ClassName.ShouldBe("ApiTests.Health");
        r2.Name.ShouldBe("Health check TC-42");
        r2.Outcome.ShouldBe(TestOutcome.Failed);
        r2.DurationSeconds!.Value.ShouldBe(0.045, 0.000_001);
        r2.ErrorMessage.ShouldBe("Assertion failed");
        r2.ErrorDetails.ShouldNotBeNull();
        r2.ErrorDetails!.ShouldContain("Expected 200 OK but was 500 Internal Server Error");
        r2.StartedAt.ShouldBe(suiteTs);
        r2.FinishedAt.ShouldBe(suiteTs + TimeSpan.FromSeconds(0.045));

        var r3 = results[2];
        r3.ClassName.ShouldBe("CalcTests.Divide");
        r3.Name.ShouldBe("Divide by zero [TC:777]");
        r3.Outcome.ShouldBe(TestOutcome.Error);
        r3.DurationSeconds!.Value.ShouldBe(0.001, 0.000_001);
        r3.ErrorMessage.ShouldStartWith("System.DivideByZeroException");
        r3.ErrorDetails.ShouldBeNull();
        r3.StartedAt.ShouldBe(suiteTs);
        r3.FinishedAt.ShouldBe(suiteTs + TimeSpan.FromSeconds(0.001));

        var r4 = results[3];
        r4.ClassName.ShouldBe("FeatureX.Skips");
        r4.Name.ShouldBe("Skipped case TC 999");
        r4.Outcome.ShouldBe(TestOutcome.Skipped);
        r4.DurationSeconds!.Value.ShouldBe(0.0, 0.000_001);
        r4.ErrorMessage.ShouldBe("Flaky test quarantine");
        r4.ErrorDetails.ShouldBeNull();
        r4.StartedAt.ShouldBe(suiteTs);
        r4.FinishedAt.ShouldBe(suiteTs + TimeSpan.FromSeconds(0.0));
    }
}

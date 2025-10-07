namespace JUnitXmlImporter3.Domain;

public sealed class TestCaseResult
{
    public required string ClassName { get; init; }
    public required string Name { get; init; }
    public double? DurationSeconds { get; init; }
    /// <summary>
    /// The outcome of the test case execution.
    /// Possible values are defined by the <see cref="TestOutcome"/> enum, such as Passed, Failed, Skipped, etc.
    /// <summary>
    public TestOutcome Outcome { get; init; }

    /// The UTC timestamp when the test case started execution. Null if not available.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// The UTC timestamp when the test case finished execution. Null if not available.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorDetails { get; init; }
}

namespace JUnitXmlImporter3.Aqua;

public sealed class AquaExecutionRequest
{
    public required int TestCaseId { get; init; }
    public required string Status { get; init; }
    public int? DurationMs { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public string? ExternalRunId { get; init; }
    public string? RunName { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorDetails { get; init; }
    public int? ProjectId { get; init; }
    // The ScenarioId provides context for the test execution within Aqua's API wire payload.
    // It identifies the specific scenario associated with this test case, ensuring correct mapping in Aqua.
    public int ScenarioId { get; init; }
}

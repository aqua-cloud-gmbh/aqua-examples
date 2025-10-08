namespace JUnitXmlImporter3.Services;

/// <summary>
/// Resolves an Aqua Test Scenario ID from provided text (typically the test case name/display name).
/// Returns null when no valid ID can be derived.
/// </summary>
public interface IScenarioIdResolver
{
    int? ResolveId(string text);
}

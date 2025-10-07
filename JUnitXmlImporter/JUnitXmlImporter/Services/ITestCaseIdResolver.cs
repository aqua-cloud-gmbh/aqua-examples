namespace JUnitXmlImporter3.Services;

/// <summary>
/// Resolves an Aqua Test Case ID from a provided text (typically the test case name/display name).
/// Returns null when no valid ID can be derived.
/// </summary>
public interface ITestCaseIdResolver
{
    int? ResolveId(string text);
}

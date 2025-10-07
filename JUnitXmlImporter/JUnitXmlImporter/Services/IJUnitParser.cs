using JUnitXmlImporter3.Domain;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Parses JUnit XML result files and produces a sequence of TestCaseResult records.
/// Supports common variants such as Maven Surefire and JUnit Platform.
/// </summary>
public interface IJUnitParser
{
    /// <summary>
    /// Parses a JUnit XML file at the specified path.
    /// </summary>
    /// <param name="path">Absolute file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed test case results from the file.</returns>
    Task<IReadOnlyList<TestCaseResult>> ParseAsync(string path, CancellationToken cancellationToken);
}

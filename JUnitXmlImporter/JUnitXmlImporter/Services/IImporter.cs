namespace JUnitXmlImporter3.Services;

/// <summary>
/// Orchestrates the import pipeline: discover inputs, parse JUnit XML, resolve Aqua test case IDs,
/// apply behavior flags, and (optionally) submit to Aqua.
/// Returns an exit code according to policy.
/// </summary>
public interface IImporter
{
    /// <summary>
    /// Executes the import pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Process exit code (0 on success, 2 on non-fatal mapping/parsing issues).</returns>
    Task<int> RunAsync(CancellationToken cancellationToken);
}

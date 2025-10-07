namespace JUnitXmlImporter3.Services;

/// <summary>
/// Discovers input JUnit XML files from configured paths and optional stdin.
/// </summary>
public interface IInputDiscovery
{
    Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken);
}

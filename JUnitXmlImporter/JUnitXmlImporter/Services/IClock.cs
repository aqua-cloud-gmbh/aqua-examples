namespace JUnitXmlImporter3.Services;

/// <summary>
/// Abstracts time retrieval for deterministic tests.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now { get; }
}

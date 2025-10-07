namespace JUnitXmlImporter3.Options;

/// <summary>
/// Aqua connection and scoping options. Values may come from CLI/JSON/env; validated at startup.
/// </summary>
public sealed class AquaOptions
{
    public string? BaseUrl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public int? ProjectId { get; init; }
}

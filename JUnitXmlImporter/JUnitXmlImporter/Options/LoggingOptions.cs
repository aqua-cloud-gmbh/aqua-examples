namespace JUnitXmlImporter3.Options;

/// <summary>
/// Controls logging configuration for the application.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// Minimum log level (Trace/Debug/Information/Warning/Error/Critical). Defaults to Information.
    /// </summary>
    public string Level { get; init; } = "Information";
}

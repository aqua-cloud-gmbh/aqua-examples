namespace JUnitXmlImporter3.Services;

/// <summary>
/// System-backed clock implementation.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTimeOffset Now => DateTimeOffset.Now;
}

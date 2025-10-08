namespace JUnitXmlImporter3.Aqua;

/// <summary>
/// Result of a submission attempt to Aqua.
/// </summary>
public sealed class AquaSubmitResult
{
    public required bool Success { get; init; }
    public int Posted { get; init; }
    public int Failed { get; init; }
    /// <summary>
    /// Contains an error message if <see cref="Success"/> is false; otherwise, null.
    /// The format is a plain text description of the failure.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

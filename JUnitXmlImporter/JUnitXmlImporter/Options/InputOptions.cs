namespace JUnitXmlImporter3.Options;

/// <summary>
/// Controls input discovery for JUnit XML files.
/// </summary>
public sealed class InputOptions
{
    /// <summary>
    /// Optional set of file or directory paths to search.
    /// </summary>
    public IReadOnlyList<string>? Paths { get; init; }

    /// <summary>
    /// File search pattern when scanning directories (default: "*.xml").
    /// </summary>
    public string? SearchPattern { get; init; } = "*.xml";

    /// <summary>
    /// Recurse into subdirectories when a directory path is provided.
    /// </summary>
    public bool Recursive { get; init; } = true;

    /// <summary>
    /// When true and no paths are provided, read JUnit XML content from STDIN and write to a temp file for processing.
    /// </summary>
    public bool ReadFromStdin { get; init; }
}

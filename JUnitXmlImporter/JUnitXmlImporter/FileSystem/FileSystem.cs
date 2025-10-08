namespace JUnitXmlImporter3.FileSystem;

/// <summary>
/// Concrete System.IO-backed implementation of IFileSystem.
/// </summary>
public sealed class FileSystem : IFileSystem
{
    public bool FileExists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Directory.Exists(path);
    }

    /// <summary>
    /// Enumerates files in the specified directory matching the search pattern.
    /// </summary>
    /// <param name="directoryPath">The path to the directory.</param>
    /// <param name="searchPattern">The search pattern to match files.</param>
    /// <param name="recursive">Whether to search subdirectories recursively.</param>
    /// <returns>An enumerable collection of file paths.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="directoryPath"/> or <paramref name="searchPattern"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown if the specified directory does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the caller does not have the required permission.</exception>
    public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(searchPattern);
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(directoryPath, searchPattern, option);
    }

    public string GetFullPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Creates an empty temporary file and returns its full path.
    /// </summary>
    public string CreateTempFile()
    {
        var path = Path.GetTempFileName();
        return path;
    }

    public void WriteAllText(string path, string? contents)
    {
        ArgumentNullException.ThrowIfNull(path);
        // Assign string.Empty to contents if null for explicit intent, though File.WriteAllText handles null by writing an empty file.
        contents = contents ?? string.Empty;
        File.WriteAllText(path, contents);
    }

    public Stream OpenRead(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return File.OpenRead(path);
    }
}

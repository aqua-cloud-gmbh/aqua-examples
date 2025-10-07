namespace JUnitXmlImporter3.FileSystem;

/// <summary>
/// Abstraction over file system operations for testability.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive);
    string GetFullPath(string path);
    string CreateTempFile();
    void WriteAllText(string path, string? contents);
    /// <summary>
    /// Opens the specified file for reading and returns a read-only stream.
    /// </summary>
    /// <param name="path">The path of the file to open.</param>
    /// <returns>A read-only stream for the specified file.</returns>
    Stream OpenRead(string path);
}

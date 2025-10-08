using System.Text.RegularExpressions;
using JUnitXmlImporter3.FileSystem;
using JUnitXmlImporter3.Options;
using JUnitXmlImporter3.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class InputDiscoveryTests
{
    [Test]
    public async Task DiscoverAsync_FilesAndDirectories_WithPatternAndRecursion_Deduplicates()
    {
        // Arrange
        var fs = new FakeFileSystem();
        fs.AddFile("C:/repo/a.xml");
        fs.AddFile("C:/repo/a.txt");
        fs.AddDirectory("C:/repo/dir");
        fs.AddFile("C:/repo/dir/b.xml");
        fs.AddDirectory("C:/repo/dir/deeper");
        fs.AddFile("C:/repo/dir/deeper/c.xml");
        fs.AddFile("C:/repo/dir/deeper/c.xml"); // duplicate

        var options = new InputOptions
        {
            Paths = new List<string> { "C:/repo/a.xml", "C:/repo/dir" },
            SearchPattern = "*.xml",
            Recursive = true,
            ReadFromStdin = false
        };

        var sut = new InputDiscovery(options, fs, NullLogger<InputDiscovery>.Instance);

        // Act
        var result = await sut.DiscoverAsync(CancellationToken.None);

        // Assert
        result.OrderBy(x => x).ToArray().ShouldBe([
            fs.GetFullPath("C:/repo/a.xml"),
            fs.GetFullPath("C:/repo/dir/b.xml"),
            fs.GetFullPath("C:/repo/dir/deeper/c.xml")
        ]);
    }

    [Test]
    public async Task DiscoverAsync_FileNotMatchingPattern_IsIgnored()
    {
        var fs = new FakeFileSystem();
        fs.AddFile("C:/repo/a.txt");
        var options = new InputOptions
        {
            Paths = new List<string> { "C:/repo/a.txt" },
            SearchPattern = "*.xml"
        };
        var sut = new InputDiscovery(options, fs, NullLogger<InputDiscovery>.Instance);
        var result = await sut.DiscoverAsync(CancellationToken.None);
        result.Count.ShouldBe(0);
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase);

        public void AddFile(string path)
        {
            _files.Add(GetFullPath(path));
            var dir = Path.GetDirectoryName(GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) _dirs.Add(dir);
        }

        public void AddDirectory(string path) => _dirs.Add(GetFullPath(path));

        public bool FileExists(string path) => _files.Contains(GetFullPath(path));

        public bool DirectoryExists(string path) => _dirs.Contains(GetFullPath(path));

        public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, bool recursive)
        {
            var root = GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar);
            foreach (var f in _files)
            {
                var inDir = f.StartsWith(root + Path.DirectorySeparatorChar);
                if (!inDir) continue;
                if (!recursive && Path.GetDirectoryName(f) != root) continue;
                var name = Path.GetFileName(f);
                if (WildcardMatch(name, searchPattern)) yield return f;
            }
        }

        private static bool WildcardMatch(string fileName, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern == "*") return true;
            var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
            return regex.IsMatch(fileName);
        }

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public string CreateTempFile() => GetFullPath(Path.GetTempFileName());

        public void WriteAllText(string path, string? contents) => File.WriteAllText(path, contents);

        public Stream OpenRead(string path) => File.OpenRead(path);
    }
}

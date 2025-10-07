using System.Text;
using System.Text.RegularExpressions;
using JUnitXmlImporter3.FileSystem;
using JUnitXmlImporter3.Logging;
using JUnitXmlImporter3.Options;
using Microsoft.Extensions.Logging;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Discovers input JUnit XML files from configured paths and optional stdin.
/// </summary>
public sealed class InputDiscovery(InputOptions options, IFileSystem fs, ILogger<InputDiscovery> logger)
    : IInputDiscovery
{
    private readonly InputOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IFileSystem _fs = fs ?? throw new ArgumentNullException(nameof(fs));
    private readonly ILogger<InputDiscovery> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pattern = _options.SearchPattern ?? "*.xml";

        if (_options.Paths is { Count: > 0 })
        {
            foreach (var raw in _options.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var path = _fs.GetFullPath(raw);

                if (_fs.FileExists(path))
                {
                    if (FileMatchesPattern(path, pattern))
                    {
                        results.Add(path);
                    }
                    else
                    {
                        _logger.LogDebug("File '{Path}' ignored due to search pattern '{Pattern}'", path, pattern);
                    }
                }
                else if (_fs.DirectoryExists(path))
                {
                    foreach (var file in _fs.EnumerateFiles(path, pattern, _options.Recursive))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        results.Add(_fs.GetFullPath(file));
                    }
                }
                else
                {
                    _logger.LogWarning("Input path not found: {Path}", path);
                }
            }
        }
        else if (_options.ReadFromStdin)
        {
            if (!Console.IsInputRedirected)
            {
                _logger.LogWarning("ReadFromStdin enabled but STDIN is not redirected. Skipping STDIN.");
            }
            else
            {
                using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
                var content = await reader.ReadToEndAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("STDIN was empty; no inputs discovered.");
                }
                else
                {
                    var temp = _fs.CreateTempFile();
                    _fs.WriteAllText(temp, content);
                    results.Add(temp);
                    _logger.LogInformation("Captured STDIN into temp file: {Path}", temp);
                }
            }
        }

        var count = results.Count;
        _logger.LogInformation("Discovered {Count} input file(s) with pattern '{Pattern}' (recursive={Recursive}).", count, pattern, _options.Recursive);
        _logger.LogDebug("Input files: {List}", SecretRedactor.Redact(string.Join(", ", results)));
        return results.ToList();
    }

    private static bool FileMatchesPattern(string filePath, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*") return true;
        // Convert wildcard pattern to regex
        var regex = WildcardToRegex(pattern);
        var fileName = Path.GetFileName(filePath);
        return regex.IsMatch(fileName);
    }

    private static Regex WildcardToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}

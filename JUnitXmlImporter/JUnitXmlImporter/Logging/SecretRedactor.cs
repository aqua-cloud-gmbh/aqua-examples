using System.Text.RegularExpressions;

namespace JUnitXmlImporter3.Logging;

/// <summary>
/// Provides redaction utilities to mask secrets in log messages.
/// </summary>
public static class SecretRedactor
{
    private static readonly Regex PasswordPattern = new("(password|pwd)\\s*[:=]\\s*([^\\s\"']+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TokenPattern = new("(token)\\s*[:=]\\s*([^\\s\"']+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BearerPattern = new("(authorization)\\s*[:=]?\\s*Bearer\\s+([A-Za-z0-9\\-._~+/]+=*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UsernamePattern = new("(username|user)\\s*[:=]\\s*([^\\s\"']+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Redacts sensitive information such as passwords, tokens, bearer tokens, and usernames from the input string.
    /// </summary>
    /// <param name="input">The input string potentially containing secrets to be redacted.</param>
    /// <returns>The input string with sensitive information replaced by &lt;redacted&gt;.</returns>
    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        var s = input;
        s = PasswordPattern.Replace(s, m => $"{m.Groups[1].Value}: <redacted>");
        s = TokenPattern.Replace(s, m => $"{m.Groups[1].Value}: <redacted>");
        s = BearerPattern.Replace(s, m => $"{m.Groups[1].Value}: Bearer <redacted>");
        s = UsernamePattern.Replace(s, m => $"{m.Groups[1].Value}: <redacted>");
        return s;
    }
}

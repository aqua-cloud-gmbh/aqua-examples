using Microsoft.Extensions.Logging;

namespace JUnitXmlImporter3.Logging;

/// <summary>
/// ILoggerProvider that redacts secrets from messages and exceptions before writing to the console.
/// </summary>
public sealed class RedactingLoggerProvider(LogLevel minimumLevel, string defaultCategory = "JUnitXmlImporter.General") : ILoggerProvider
{
    private readonly string _defaultCategory = defaultCategory;
    public ILogger CreateLogger(string categoryName) => new RedactingLogger(categoryName, minimumLevel, _defaultCategory);

    public void Dispose()
    {
        // nothing to dispose
    }

    private sealed class RedactingLogger(string? category, LogLevel min, string defaultCategory) : ILogger
    {
        private readonly string _category = string.IsNullOrWhiteSpace(category) ? defaultCategory : category;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= min;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string>? formatter)
        {
            if (formatter is null) return;
            var message = formatter(state, exception);
            message = SecretRedactor.Redact(message);
            var ex = exception?.ToString();
            ex = ex is null ? null : SecretRedactor.Redact(ex);

            var timestamp = DateTimeOffset.UtcNow.ToString("O");
            var level = logLevel.ToString();
            Console.WriteLine($"{timestamp} [{level}] {_category}: {message}");
            if (!string.IsNullOrWhiteSpace(ex))
            {
                Console.WriteLine(ex);
            }
        }
        /// <summary>
        /// A no-op scope implementation for logging, used when no actual scope is required.
        /// </summary>
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}

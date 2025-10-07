using System.Text.RegularExpressions;
using JUnitXmlImporter3;
using JUnitXmlImporter3.Aqua;
using JUnitXmlImporter3.FileSystem;
using JUnitXmlImporter3.Logging;
using JUnitXmlImporter3.Options;
using JUnitXmlImporter3.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
// Telemetry

// Build configuration with precedence: defaults (class init defaults) < env vars < JSON file < CLI args
var configuration = BuildConfiguration(args);

// Bind options (will be used by DI and application services in subsequent tasks)
var aquaOptions = configuration.GetSection("aqua").Get<AquaOptions>() ?? new AquaOptions();
var mappingOptions = configuration.GetSection("mapping").Get<MappingOptions>() ?? new MappingOptions();
var behaviorOptions = configuration.GetSection("behavior").Get<BehaviorOptions>() ?? new BehaviorOptions();
var httpOptions = configuration.GetSection("http").Get<HttpOptions>() ?? new HttpOptions();
var inputOptions = configuration.GetSection("input").Get<InputOptions>() ?? new InputOptions();
var runOptions = configuration.GetSection("run").Get<RunOptions>() ?? new RunOptions();
var loggingOptions = configuration.GetSection("logging").Get<LoggingOptions>() ?? new LoggingOptions();

// Perform ${VAR} environment placeholder substitution in option strings
(aquaOptions, mappingOptions, inputOptions, runOptions, loggingOptions) = SubstituteEnvPlaceholders(
    aquaOptions, mappingOptions, inputOptions, runOptions, loggingOptions);

// Validate configuration before building host
if (!ValidateConfiguration(aquaOptions, behaviorOptions, httpOptions))
{
    Environment.ExitCode = 4;
    return;
}

// Create host and configure DI + logging
var app = Host.CreateApplicationBuilder(args);

// OpenTelemetry: tracing and metrics
app.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: Telemetry.ServiceName,
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                    serviceInstanceId: Environment.MachineName))
    .WithTracing(b => b
        .AddSource(Telemetry.ServiceName)
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(b => b
        .AddMeter(Telemetry.ServiceName)
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

// Register configuration and options
app.Services.AddSingleton(configuration);
app.Services.AddSingleton(aquaOptions);
app.Services.AddSingleton(mappingOptions);
app.Services.AddSingleton(behaviorOptions);
app.Services.AddSingleton(httpOptions);
app.Services.AddSingleton(inputOptions);
app.Services.AddSingleton(runOptions);
app.Services.AddSingleton(loggingOptions);

// HTTP factory and core services
app.Services.AddHttpClient("Aqua", client =>
{
    if (string.IsNullOrWhiteSpace(aquaOptions.BaseUrl))
    {
        throw new InvalidOperationException("Aqua BaseUrl is required.");
    }
    if (!Uri.TryCreate(aquaOptions.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException("Aqua BaseUrl must be a valid HTTPS URL.");
    }
    client.BaseAddress = baseUri;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, httpOptions.TimeoutSeconds));
});

app.Services.AddSingleton<IFileSystem, FileSystem>();
app.Services.AddSingleton<IInputDiscovery, InputDiscovery>();
app.Services.AddSingleton<IJUnitParser, JUnitParser>();
app.Services.AddSingleton<IClock, SystemClock>();
app.Services.AddSingleton<IAquaClient, AquaClient>();

// Composite options and infrastructure for Importer
app.Services.AddSingleton(new ImporterOptions(behaviorOptions, runOptions, aquaOptions));
app.Services.AddSingleton(sp => new ImporterInfrastructure(
    sp.GetRequiredService<IClock>(),
    sp.GetRequiredService<IAquaClient>(),
    sp.GetRequiredService<ILogger<Importer>>()));

app.Services.AddSingleton<IImporter, Importer>();

// ID resolver strategy registration
// When strategy is "strict-ts-tc" use strict TS/TC six-digit resolvers per plan; otherwise retain existing behavior.
if (string.Equals(mappingOptions.Strategy, "prefix-suffix", StringComparison.OrdinalIgnoreCase))
{
    app.Services.AddSingleton<ITestCaseIdResolver, PrefixSuffixTestCaseIdResolver>();
    app.Services.AddSingleton<IScenarioIdResolver>(_ => new RegexScenarioIdResolver());
}
else if (string.Equals(mappingOptions.Strategy, "strict-ts-tc", StringComparison.OrdinalIgnoreCase))
{
    app.Services.AddSingleton<ITestCaseIdResolver>(_ => new RegexStrictTestCaseIdResolver());
    app.Services.AddSingleton<IScenarioIdResolver>(_ => new RegexScenarioIdResolver());
}
else
{
    app.Services.AddSingleton<ITestCaseIdResolver, RegexTestCaseIdResolver>();
    app.Services.AddSingleton<IScenarioIdResolver>(_ => new RegexScenarioIdResolver());
}

// Configure logging
app.Logging.ClearProviders();
var minLevel = ParseLogLevel(loggingOptions.Level);
app.Logging.SetMinimumLevel(minLevel);

// Redacting console logger for human-readable output
app.Logging.AddProvider(new RedactingLoggerProvider(minLevel));

// OpenTelemetry logging provider (exports logs to OTLP endpoint; optional console exporter via env var OTEL_LOGS_CONSOLE=true)
app.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true;

    // Export to OTLP if configured via environment (OTEL_EXPORTER_OTLP_ENDPOINT etc.)
    options.AddOtlpExporter();

    // Optional console export for diagnostics
    if (string.Equals(Environment.GetEnvironmentVariable("OTEL_LOGS_CONSOLE"), "true", StringComparison.OrdinalIgnoreCase))
    {
        options.AddConsoleExporter();
    }
});

using IHost host = app.Build();

// Ensure OpenTelemetry TracerProvider is created at startup
host.Services.GetService<TracerProvider>();
// Ensure OpenTelemetry MeterProvider is created at startup
var meterProvider = host.Services.GetService<MeterProvider>();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
logger.LogInformation("Configuration loaded. Aqua BaseUrl: {BaseUrl}, Timeout: {Timeout}s", aquaOptions.BaseUrl ?? "<not set>", httpOptions.TimeoutSeconds);

using var cts = new CancellationTokenSource();
// ReSharper disable once AccessToDisposedClosure
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

int exitCode;
try
{
    var importer = host.Services.GetRequiredService<IImporter>();
    exitCode = await importer.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    logger.LogWarning("Import canceled by user.");
    exitCode = 0; // graceful cancellation
}
catch (Exception ex)
{
    logger.LogError(ex, "Unhandled error during import.");
    exitCode = 4; // configuration or unexpected errors
}

Environment.ExitCode = exitCode;

meterProvider?.ForceFlush();
await host.StopAsync();

static IConfigurationRoot BuildConfiguration(string[] args)
{
    string? configPath = GetConfigPath(args);

    var switchMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"--aqua-url", "aqua:baseUrl"},
        {"--username", "aqua:username"},
        {"--password", "aqua:password"},
        {"--project", "aqua:projectId"},
        {"--regex", "mapping:pattern"},
        {"--skip-unmapped", "behavior:skipUnmapped"},
        {"--fail-on-unmapped", "behavior:failOnUnmapped"},
        {"--dry-run", "behavior:dryRun"},
        {"--timeout", "http:timeoutSeconds"},
        {"--retries", "http:retries"},
        {"--input", "input:paths:0"},
        {"-i", "input:paths:0"},
        {"--search-pattern", "input:searchPattern"},
        {"--recursive", "input:recursive"},
        {"--stdin", "input:readFromStdin"},
        {"--run-name", "run:name"},
        {"--log-level", "logging:level"}
    };

    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddEnvironmentVariables();

    if (!string.IsNullOrWhiteSpace(configPath))
    {
        builder = builder.AddJsonFile(configPath, optional: true, reloadOnChange: false);
    }

    builder = builder.AddCommandLine(args, switchMap);

    return builder.Build();
}

static string? GetConfigPath(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "-c", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }
    }

    return null;
}

static (AquaOptions aqua, MappingOptions mapping, InputOptions input, RunOptions run, LoggingOptions logging) SubstituteEnvPlaceholders(
    AquaOptions aqua, MappingOptions mapping, InputOptions input, RunOptions run, LoggingOptions logging)
{
    string? S(string? value) => value is null ? null : Regex.Replace(value, "\\$\\{([A-Za-z_][A-Za-z0-9_]*)\\}", m =>
    {
        var key = m.Groups[1].Value;
        var env = Environment.GetEnvironmentVariable(key);
        return env ?? m.Value; // keep placeholder if env not set
    });

    var paths = input.Paths is null ? null : input.Paths.Select(p => S(p) ?? p).ToList();

    var newAqua = new AquaOptions
    {
        BaseUrl = S(aqua.BaseUrl),
        Username = S(aqua.Username),
        Password = S(aqua.Password),
        ProjectId = aqua.ProjectId
    };

    var newMapping = new MappingOptions
    {
        Strategy = mapping.Strategy,
        Pattern = S(mapping.Pattern),
        Prefix = S(mapping.Prefix),
        DigitsLength = mapping.DigitsLength
    };

    var newInput = new InputOptions
    {
        Paths = paths,
        SearchPattern = S(input.SearchPattern) ?? input.SearchPattern,
        Recursive = input.Recursive,
        ReadFromStdin = input.ReadFromStdin
    };

    var newRun = new RunOptions
    {
        Name = S(run.Name),
        ExternalRunId = S(run.ExternalRunId)
    };

    var newLogging = new LoggingOptions
    {
        Level = S(logging.Level) ?? logging.Level
    };

    return (newAqua, newMapping, newInput, newRun, newLogging);
}

static LogLevel ParseLogLevel(string? level)
{
    if (string.IsNullOrWhiteSpace(level)) return LogLevel.Information;
    return Enum.TryParse<LogLevel>(level, ignoreCase: true, out var parsed) ? parsed : LogLevel.Information;
}

static bool ValidateConfiguration(AquaOptions aqua, BehaviorOptions behavior, HttpOptions http)
{
    var errors = new List<string>();

    if (http.TimeoutSeconds <= 0)
    {
        errors.Add("http.timeoutSeconds must be > 0.");
    }

    if (http.Retries <= 0)
    {
        errors.Add("http.retries must be > 0.");
    }

    if (!behavior.DryRun)
    {
        if (string.IsNullOrWhiteSpace(aqua.BaseUrl))
        {
            errors.Add("aqua.baseUrl is required when not running in dry-run mode.");
        }
        else if (!Uri.TryCreate(aqua.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("aqua.baseUrl must be a valid HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(aqua.Username))
        {
            errors.Add("aqua.username is required when not running in dry-run mode.");
        }

        if (string.IsNullOrWhiteSpace(aqua.Password))
        {
            errors.Add("aqua.password is required when not running in dry-run mode.");
        }
    }


    if (aqua.ProjectId is { } pid && pid <= 0)
    {
        errors.Add("aqua.projectId must be > 0 when specified.");
    }

    if (errors.Count > 0)
    {
        foreach (var e in errors)
        {
            Console.Error.WriteLine($"Configuration error: {e}");
        }

        Console.Error.WriteLine("Aborting startup due to invalid configuration. Exit code 4.");
        return false;
    }

    return true;
}

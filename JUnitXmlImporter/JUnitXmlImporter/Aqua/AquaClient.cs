using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using JUnitXmlImporter3.Options;
using Microsoft.Extensions.Logging;

namespace JUnitXmlImporter3.Aqua;

internal sealed class AquaClient : IAquaClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AquaOptions _aqua;
    private readonly HttpOptions _http;
    private readonly ILogger<AquaClient> _logger;

    private string? _bearerToken;

    public AquaClient(AquaOptions aqua, HttpOptions? http, ILogger<AquaClient> logger, IHttpClientFactory httpClientFactory)
    {
        _aqua = aqua ?? throw new ArgumentNullException(nameof(aqua));
        _http = http ?? new HttpOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        if (string.IsNullOrWhiteSpace(_aqua.BaseUrl))
        {
            throw new InvalidOperationException("Aqua BaseUrl is required.");
        }

        if (!Uri.TryCreate(_aqua.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Aqua BaseUrl must be a valid HTTPS URL.");
        }
        // HttpClient configuration is provided via IHttpClientFactory (named client) in Program.cs
    }

    internal AquaClient(AquaOptions aqua, HttpOptions? http, ILogger<AquaClient> logger, HttpMessageHandler handler)
    {
        _aqua = aqua ?? throw new ArgumentNullException(nameof(aqua));
        _http = http ?? new HttpOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_aqua.BaseUrl))
        {
            throw new InvalidOperationException("Aqua BaseUrl is required.");
        }

        if (!Uri.TryCreate(_aqua.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Aqua BaseUrl must be a valid HTTPS URL.");
        }

        _httpClientFactory = new HandlerBackedClientFactory(handler, baseUri, TimeSpan.FromSeconds(Math.Max(1, _http.TimeoutSeconds)));
    }

    public async Task<AquaSubmitResult> SubmitExecutionsAsync(IReadOnlyList<AquaExecutionRequest>? executions, CancellationToken cancellationToken)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        using var activity = Telemetry.ActivitySource.StartActivity("Aqua.SubmitExecutions", ActivityKind.Client);
        activity?.SetTag("aqua.executions.count", executions?.Count ?? 0);

        ArgumentNullException.ThrowIfNull(executions);
        if (executions.Count == 0)
        {
            return new AquaSubmitResult { Success = true, Posted = 0, Failed = 0 };
        }

        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var json = SerializePayload(BuildPayload(executions));
        activity?.SetTag("aqua.payload.size_bytes", Encoding.UTF8.GetByteCount(json));

        var attempts = Math.Max(1, _http.Retries);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = CreatePostRequest(json);

            HttpResponseMessage? response;
            try
            {
                var httpClient = GetHttpClient();
                response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                activity?.AddEvent(new ActivityEvent("timeout", tags: new ActivityTagsCollection
                {
                    ["attempt"] = attempt,
                    ["attempts"] = attempts
                }));
                _logger.LogWarning("HTTP timeout when posting test executions (attempt {Attempt}/{Attempts}).", attempt, attempts);
                if (attempt == attempts)
                {
                    return new AquaSubmitResult { Success = false, Posted = 0, Failed = executions.Count, ErrorMessage = "Timeout" };
                }
                await DelayWithBackoffAsync(attempt, null, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                var (completed, result, retryAfter) = await HandlePostResponseAsync(response, executions.Count, cancellationToken).ConfigureAwait(false);
                if (completed)
                {
                    if (result is { Success: true })
                    {
                        activity?.SetStatus(ActivityStatusCode.Ok);
                    }
                    else
                    {
                        activity?.SetStatus(ActivityStatusCode.Error, result?.ErrorMessage);
                    }
                    return result!;
                }

                // transient -> retry path
                activity?.AddEvent(new ActivityEvent("transient_error", tags: new ActivityTagsCollection
                {
                    ["status"] = (int)response.StatusCode,
                    ["attempt"] = attempt,
                    ["attempts"] = attempts,
                    ["retry_after_ms"] = (int?)retryAfter?.TotalMilliseconds
                }));
                _logger.LogWarning("Transient HTTP {Status} when posting executions (attempt {Attempt}/{Attempts}).", (int)response.StatusCode, attempt, attempts);
                if (attempt == attempts)
                {
                    return result!; // The result is final failure when no attempts left
                }

                await DelayWithBackoffAsync(attempt, retryAfter, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                response.Dispose();
            }
        }

        return new AquaSubmitResult { Success = false, Posted = 0, Failed = executions.Count, ErrorMessage = "Unknown error" };
    }

    private AquaExecutionDto[] BuildPayload(IReadOnlyList<AquaExecutionRequest> executions)
    {
        // Map to wire payload per PRD Section 7.
        return executions.Select((e, i) => new AquaExecutionDto
        {
            TestCaseId = e.TestCaseId,
            Steps = [new TestStepDto { Index = 1, Status = e.Status }],
            ExecutionDuration = e.DurationMs.HasValue
                ? new ExecutionDurationDto
                {
                    FieldValueType = "TimeSpan",
                    Value = e.DurationMs.Value / 1000.0, // Convert ms to seconds
                    Unit = "Second"
                }
                : null,
            StartedAt = e.StartedAt,
            FinishedAt = e.FinishedAt,
            ExternalRunId = e.ExternalRunId,
            RunName = e.RunName,
            ErrorMessage = e.ErrorMessage,
            ErrorDetails = e.ErrorDetails,
            ProjectId = e.ProjectId,
            TestScenarioInfo = new TestScenarioInfoDto
            {
                Index = i + 1,
                TestScenarioId = e.ScenarioId,
                TestJobId = i + 1
            }
        }).ToArray();
    }

    private static string SerializePayload(AquaExecutionDto[] payload)
    {
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private HttpRequestMessage CreatePostRequest(string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/TestExecution");
        var token = Volatile.Read(ref _bearerToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return request;
    }

    private async Task<(bool completed, AquaSubmitResult? result, TimeSpan? retryAfter)> HandlePostResponseAsync(
        HttpResponseMessage response,
        int executionCount,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Submitted {Count} execution(s) to Aqua. Status: {StatusCode}", executionCount, (int)response.StatusCode);
            return (true, new AquaSubmitResult { Success = true, Posted = executionCount, Failed = 0 }, null);
        }

        var statusCodeInt = (int)response.StatusCode;
        if (statusCodeInt == 429 || statusCodeInt >= 500)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta;
            // Do not log here to keep attempt counters accurate; The caller will log the transient status.
            return (false, new AquaSubmitResult { Success = false, Posted = 0, Failed = executionCount, ErrorMessage = $"HTTP {statusCodeInt}" }, retryAfter);
        }

        // Non-retryable
        var bodySnippet = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        _logger.LogError("Failed to post executions. Status: {Status}. Body: {Body}", statusCodeInt, bodySnippet);
        return (true, new AquaSubmitResult { Success = false, Posted = 0, Failed = executionCount, ErrorMessage = $"HTTP {statusCodeInt}" }, null);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Volatile.Read(ref _bearerToken))) return;

        // ReSharper disable once ExplicitCallerInfoArgument
        using var activity = Telemetry.ActivitySource.StartActivity("Aqua.Authenticate", ActivityKind.Client);

        if (string.IsNullOrWhiteSpace(_aqua.Username) || string.IsNullOrWhiteSpace(_aqua.Password))
        {
            var ex = new InvalidOperationException("Aqua credentials are required.");
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw ex;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/token");
        request.Content = new StringContent($"grant_type=password&username={Uri.EscapeDataString(_aqua.Username)}&password={Uri.EscapeDataString(_aqua.Password)}", Encoding.UTF8, "application/x-www-form-urlencoded");

        var httpClient = GetHttpClient();
        HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var snippet = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError("Authentication failed with status {Status}. Body: {Body}", (int)response.StatusCode, snippet);
            var ex = new InvalidOperationException($"Authentication failed with status {(int)response.StatusCode}");
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw ex;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        string? token = null;
        if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
        {
            token = tokenProp.GetString();
        }
        else if (doc.RootElement.TryGetProperty("token", out var tokenProp2))
        {
            token = tokenProp2.GetString();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            var ex = new InvalidOperationException("Authentication response did not contain a bearer token.");
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw ex;
        }

        Volatile.Write(ref _bearerToken, token);

        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var s = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (s.Length > 500) s = s.Substring(0, 500) + "...";
            return s;
        }
        catch
        {
            return "<no body>";
        }
    }

    private static async Task DelayWithBackoffAsync(int attempt, TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        if (retryAfter is { } ra && ra > TimeSpan.Zero)
        {
            await Task.Delay(ra, cancellationToken).ConfigureAwait(false);
            return;
        }

        var delayMs = (int)Math.Min(30000, Math.Pow(2, attempt - 1) * 1000); // 1s, 2s, 4s, ...
        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private HttpClient GetHttpClient()
    {
        return _httpClientFactory.CreateClient("Aqua");
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class AquaExecutionDto
    {
        public int TestCaseId { get; init; }
        public TestStepDto[]? Steps { get; init; }
        public object? ExecutionDuration { get; init; }
        public DateTimeOffset? StartedAt { get; init; }
        public DateTimeOffset? FinishedAt { get; init; }
        public string? ExternalRunId { get; init; }
        public string? RunName { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorDetails { get; init; }
        public int? ProjectId { get; init; }
        public TestScenarioInfoDto? TestScenarioInfo { get; init; }
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class TestStepDto
    {
        public int Index { get; set; }
        public string? Status { get; init; }
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class TestScenarioInfoDto
    {
        public int Index { get; init; }
        // ReSharper disable once UnusedMember.Local
        public RunDependencyDto[]? RunDependency { get; init; }
        public int TestScenarioId { get; init; }
        public int TestJobId { get; init; }
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private sealed class RunDependencyDto
    {
        public int RunIndex { get; init; }
        public bool OnSuccessOnly { get; init; }
    }

    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class ExecutionDurationDto
    {
        public string FieldValueType { get; init; } = null!;
        public double Value { get; init; }
        public string Unit { get; init; } = null!;
    }

    private sealed class HandlerBackedClientFactory(HttpMessageHandler handler, Uri baseUri, TimeSpan timeout)
        : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        private readonly Uri _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));

        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = _baseUri,
                Timeout = timeout
            };
            return client;
        }
    }
}

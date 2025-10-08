using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JUnitXmlImporter3.Aqua;
using JUnitXmlImporter3.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shouldly;

namespace JUnitXmlImporter3.Tests;

[TestFixture]
public class AquaClientTests
{
    [Test]
    public async Task SubmitExecutionsAsync_SendsBearerAndJsonPayload()
    {
        var handler = new SequenceHandler(
            // 1) token
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"abc123\"}", Encoding.UTF8, "application/json")
            },
            // 2) submit
            new HttpResponseMessage(HttpStatusCode.OK)
        );

        var aqua = new AquaOptions { BaseUrl = "https://example.com", Username = "user", Password = "pass" };
        var http = new HttpOptions { TimeoutSeconds = 10, Retries = 1 };
        var client = new AquaClient(aqua, http, NullLogger<AquaClient>.Instance, handler);

        var result = await client.SubmitExecutionsAsync([
            new AquaExecutionRequest { TestCaseId = 42, Status = "Pass", DurationMs = 1500, StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow }
        ], CancellationToken.None);

        result.Success.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(2);

        // Inspect second request
        var submitReq = handler.Requests[1];
        submitReq.Method.ShouldBe(HttpMethod.Post);
        submitReq.RequestUri!.ToString().ShouldBe("https://example.com/api/TestExecution");
        submitReq.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Bearer", "abc123"));

        var json = await submitReq.Content!.ReadAsStringAsync();
        // The payload should be a JSON array
        JsonDocument.Parse(json).RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Test]
    public async Task SubmitExecutionsAsync_MapsPayloadFields()
    {
        var token = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"abc\"}")
        };
        var ok = new HttpResponseMessage(HttpStatusCode.OK);
        var handler = new SequenceHandler(token, ok);

        var aqua = new AquaOptions { BaseUrl = "https://example.com", Username = "user", Password = "pass" };
        var http = new HttpOptions { TimeoutSeconds = 10, Retries = 1 };
        var client = new AquaClient(aqua, http, NullLogger<AquaClient>.Instance, handler);

        var started = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        var finished = started.AddSeconds(2);
        var req = new AquaExecutionRequest
        {
            TestCaseId = 100,
            Status = "Pass",
            DurationMs = 1500,
            StartedAt = started,
            FinishedAt = finished,
            ExternalRunId = "run-1",
            RunName = "My Run",
            ErrorMessage = "msg",
            ErrorDetails = "details",
            ProjectId = 7,
            ScenarioId = 77
        };

        var result = await client.SubmitExecutionsAsync([req], CancellationToken.None);
        result.Success.ShouldBeTrue();

        var submitReq = handler.Requests[1];
        submitReq.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Bearer", "abc"));
        var json = await submitReq.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var elem = doc.RootElement[0];
        elem.GetProperty("testCaseId").GetInt32().ShouldBe(100);
        elem.TryGetProperty("status", out _).ShouldBeFalse();
        var steps = elem.GetProperty("steps");
        steps.ValueKind.ShouldBe(JsonValueKind.Array);
        steps.GetArrayLength().ShouldBe(1);
        steps[0].GetProperty("status").GetString().ShouldBe("Pass");
        var dur = elem.GetProperty("executionDuration");
        dur.GetProperty("fieldValueType").GetString().ShouldBe("TimeSpan");
        dur.GetProperty("value").GetDouble().ShouldBe(1.5, 0.0001);
        dur.GetProperty("unit").GetString().ShouldBe("Second");
        elem.GetProperty("startedAt").GetDateTimeOffset().ShouldBe(started);
        elem.GetProperty("finishedAt").GetDateTimeOffset().ShouldBe(finished);
        elem.GetProperty("externalRunId").GetString().ShouldBe("run-1");
        elem.GetProperty("runName").GetString().ShouldBe("My Run");
        elem.GetProperty("errorMessage").GetString().ShouldBe("msg");
        elem.GetProperty("errorDetails").GetString().ShouldBe("details");
        elem.GetProperty("projectId").GetInt32().ShouldBe(7);
        var tsi = elem.GetProperty("testScenarioInfo");
        tsi.GetProperty("index").GetInt32().ShouldBe(1);
        tsi.GetProperty("testScenarioId").GetInt32().ShouldBe(77);
        tsi.GetProperty("testJobId").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task SubmitExecutionsAsync_AssignsIncrementingIndexAndJobId()
    {
        var token = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"tok2\"}")
        };
        var ok = new HttpResponseMessage(HttpStatusCode.OK);
        var handler = new SequenceHandler(token, ok);

        var aqua = new AquaOptions { BaseUrl = "https://example.com", Username = "u", Password = "p" };
        var http = new HttpOptions { TimeoutSeconds = 5, Retries = 1 };
        var client = new AquaClient(aqua, http, NullLogger<AquaClient>.Instance, handler);

        var reqs = new[]
        {
            new AquaExecutionRequest { TestCaseId = 10, Status = "Pass", ScenarioId = 5 },
            new AquaExecutionRequest { TestCaseId = 11, Status = "Failed", ScenarioId = 5 }
        };

        var result = await client.SubmitExecutionsAsync(reqs, CancellationToken.None);
        result.Success.ShouldBeTrue();

        var submitReq = handler.Requests[1];
        var json = await submitReq.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().ShouldBe(2);
        doc.RootElement[0].GetProperty("testScenarioInfo").GetProperty("index").GetInt32().ShouldBe(1);
        doc.RootElement[0].GetProperty("testScenarioInfo").GetProperty("testJobId").GetInt32().ShouldBe(1);
        doc.RootElement[1].GetProperty("testScenarioInfo").GetProperty("index").GetInt32().ShouldBe(2);
        doc.RootElement[1].GetProperty("testScenarioInfo").GetProperty("testJobId").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task SubmitExecutionsAsync_RetriesOn500()
    {
        var handler = new SequenceHandler(
            // token
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"t\"}")
            },
            // first submit attempt -> 500
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            // second submit attempt -> 200
            new HttpResponseMessage(HttpStatusCode.OK)
        );

        var aqua = new AquaOptions { BaseUrl = "https://example.com", Username = "u", Password = "p" };
        var http = new HttpOptions { TimeoutSeconds = 5, Retries = 2 };
        var client = new AquaClient(aqua, http, NullLogger<AquaClient>.Instance, handler);

        var result = await client.SubmitExecutionsAsync([new AquaExecutionRequest { TestCaseId = 1, Status = "Pass" }], CancellationToken.None);

        result.Success.ShouldBeTrue();
        // token + 2 submit attempts
        handler.Requests.Count.ShouldBe(3);
    }

    [Test]
    public async Task SubmitExecutionsAsync_RetriesOn429_HonorsRetryAfter()
    {
        var token = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"tok\"}")
        };
        var tooMany = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooMany.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
        var ok = new HttpResponseMessage(HttpStatusCode.OK);
        var handler = new SequenceHandler(token, tooMany, ok);

        var aqua = new AquaOptions { BaseUrl = "https://example.com", Username = "u", Password = "p" };
        var http = new HttpOptions { TimeoutSeconds = 5, Retries = 2 };
        var client = new AquaClient(aqua, http, NullLogger<AquaClient>.Instance, handler);

        var result = await client.SubmitExecutionsAsync([new AquaExecutionRequest { TestCaseId = 2, Status = "Pass" }], CancellationToken.None);

        result.Success.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(3); // token + 2 submits
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
            return Task.FromResult(_responses.Dequeue());
        }
    }
}

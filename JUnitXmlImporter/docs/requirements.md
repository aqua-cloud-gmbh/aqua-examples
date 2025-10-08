# Program Requirements Document (PRD)

Title: JUnit XML Importer for Aqua Test Executions
Version: 1.0
Date: 2025-08-20
Owner: JUnitXmlImporter3 Project

## 1. Overview
The JUnit XML Importer is a .NET 9 console application that reads JUnit XML
test results, extracts Aqua test scenario IDs and test case IDs from the test
case name fields in the XML, and creates corresponding test executions in Aqua.
Created test executions are grouped by test scenario. Each unique test scenario
results in a separate POST to Aqua’s /api/TestExecution endpoint. The tool uses
dependency injection, applies SOLID principles, and includes an automated test
suite (NUnit + Shouldly).

## 2. Goals and Non‑Goals
- Goals
  - Read one JUnit XML file.
  - Extract TestScenarioId and TestCaseId from each test case’s name using strict TS000000/TC000000 patterns (configurable via regex options).
  - For each parsed test case, create one test execution in Aqua with status, duration, error message/stack (if any), and timing.
  - Group created test executions by TestScenarioId and submit one POST /api/TestExecution per unique scenario.
  - Provide robust configuration (CLI args and optional JSON config), logging.
  - Use DI with IServiceCollection/IServiceProvider; follow SOLID principles.
  - Maintain an automated test suite (NUnit + Shouldly) for parsing, mapping, and API client behavior (mocked); tests are not part of the shipped artifact.
- Non‑Goals
  - Not a generic test management synchronizer beyond Aqua.
  - Not a full JUnit format validator; assume standard JUnit outputs.
  - Do not ship or distribute the test suite as part of the deliverable.

## 3. Stakeholders & Users
- QA Engineers / SDETs who run automated tests and publish results to Aqua.
- DevOps/CI engineers integrating the tool into pipelines (Jenkins, GitHub
  Actions, Azure DevOps, etc.).

## 4. Assumptions & Dependencies
- JUnit XML conforms to standard schema variants (e.g., surefire,
  junit-platform) with testcases/testsuite.
- The Aqua project and test cases already exist with numeric IDs.
- Authentication: obtain an access token by POSTing to /api/token
  with application/x-www-form-urlencoded body (grant_type=password,
  username, password). Use the returned token as a Bearer in the
  Authorization header for subsequent requests.
- Network access to Aqua API endpoint is available from the execution
  environment.

## 5. Definitions
- Test Case Name: The name field of JUnit <testcase> element in the XML input; may include class name and/or a display name.
- Test Scenario ID: Extracted from the test case name. The test scenario name will be TS followed immediately by a six-digit decimal number (zero-padded), e.g., TS000123; this six-digit number is the test scenario ID.
- Aqua Test Case ID: Extracted from the test case name. The test case name will be TC followed immediately by a six-digit decimal number (zero-padded), e.g., TC000456; this six-digit number is the test case ID.

## 6. Functional Requirements
1. Input Handling
   - Accept one or more input paths (files and/or directories). Directories are
     scanned recursively for files matching *.xml (configurable).
   - Support stdin piping of XML (optional, stretch goal).
2. JUnit Parsing
   - Parse the following JUnit structures:
     - testsuite (name, time, timestamp, properties)
     - testcase (classname, name, time)
     - failure/error/skipped nodes with message and content
   - Extract for each testcase: name, class, duration, outcome
     (Passed/Failed/Errored/Skipped), failure message/stack, start/end time if
     available or best-effort.
3. Test Scenario and Test Case ID Extraction
   - Source: Both IDs MUST be extracted from the JUnit <testcase> name field in the XML input file.
   - Patterns:
     - Test Scenario: name contains TS followed immediately by six decimal digits (zero-padded), e.g., TS000123. The six-digit number is the TestScenarioId.
     - Test Case: name contains TC followed immediately by six decimal digits (zero-padded), e.g., TC000456. The six-digit number is the TestCaseId.
   - Configurable strategies:
     - RegexStrategy (default):
       - TestScenarioId pattern: (?<![A-Z0-9])TS([0-9]{6})(?![0-9])
       - TestCaseId pattern: (?<![A-Z0-9])TC([0-9]{6})(?![0-9])
     - Custom strategy via extension point (DI) allowing a custom resolver implementation.
   - Validation: extracted IDs must be integers in the range [0, 999999]; leading zeros are accepted and preserved as six-digit strings when needed; numeric value is used for IDs.
   - Behavior when missing:
     - If a TestScenarioId or TestCaseId cannot be derived, behavior is configurable: skip, warn, or fail.
4. Aqua API Integration
  - Grouping and Submission: The importer MUST group created test executions by TestScenarioId. For each unique TestScenarioId, make a separate POST request to /api/TestExecution with only the executions that belong to that scenario.
  - The POST body MUST conform to Aqua’s official payload structure documented in Section 7 (Body example — official payload structure).
  - Internally, the importer maps its data model to Aqua’s fields when building the request; field names and shapes in logs/config may differ from the final payload.
  - Retries: exponential backoff on 5xx and 429; max attempts configurable.
  - Rate limiting: respect 429 Retry-After header if provided.
5. Configuration
   - Configuration precedence: CLI args > JSON config file > environment
     variables > defaults.
   - CLI options (examples):
     - --input, -i: one or more file/dir paths
     - --aqua-url: base URL (e.g., https://app.aqua-cloud.io)
     - --username: Aqua username (or set AQA_USERNAME environment var)
     - --password: Aqua password (or set AQA_PASSWORD environment var)
     - --project: Aqua project identifier (if required by API)
     - --run-name: friendly name for this import (default: derived from
       timestamp)
     - --regex: custom regex for ID extraction
     - --skip-unmapped: skip tests without IDs (default: true)
     - --fail-on-unmapped: fail process if any unmapped (default: false)
     - --dry-run: do not call Aqua; print what would be sent
     - --timeout: HTTP timeout (default: 100s)
     - --retries: max retries (default: 3)
     - --log-level: Trace/Debug/Info/Warn/Error
     - --config: path to JSON/YAML config file (JSON in v1)
   - JSON config structure (example):
     {
       "aqua": {
         "baseUrl": "https://app.aqua-cloud.io",
         "username": "${AQA_USERNAME}",
         "password": "${AQA_PASSWORD}",
         "projectId": 101
       },
       "mapping": {
         "strategy": "regex",
         "scenarioPattern": "(?<![A-Z0-9])TS([0-9]{6})(?![0-9])",
         "casePattern": "(?<![A-Z0-9])TC([0-9]{6})(?![0-9])"
       },
       "behavior": { "skipUnmapped": true, "failOnUnmapped": false },
       "http": { "timeoutSeconds": 100, "retries": 3 },
       "input": {
         "paths": ["artifacts/test-results"],
         "searchPattern": "*.xml",
         "recursive": true
       },
       "run": { "name": "Nightly Build #123" },
       "logging": { "level": "Information" }
     }
6. Logging & Telemetry
   - Structured logging with levels; include per-test context (class, name,
     resolvedId).
   - Summary at end: total files, testcases parsed, mapped, created, skipped,
     failures.
7. Error Handling
   - Distinguish between parsing errors (invalid XML), mapping errors (no ID),
     and API errors.
   - Exit codes:
     - 0: success
     - 2: parsing/mapping issues but completed (when not configured to fail)
     - 3: API failures after retries
     - 4: configuration errors (missing credentials, bad URL)
8. Performance
   - Process files streaming where possible; limit memory footprint.
9. Security
   - Credentials (username/password) should be sourced from environment variables or a secure secret store; avoid logging secrets.
   - Support HTTPS only; validate TLS certs (system defaults).
   - Mask credentials and tokens in logs.

## 7. Aqua API (to be confirmed)
- Base URL: e.g., https://app.aqua-cloud.io
- Authentication: obtain an access token by POSTing to /api/token with
  application/x-www-form-urlencoded body (grant_type=password, username,
  password). Use the access_token as a Bearer in the Authorization header
  for subsequent requests.
- Endpoint(s): subject to Aqua API; validated against
  https://app.aqua-cloud.io/aquaWebNG/help:
  - POST /api/TestExecution
  - Body example (array of executions in a single request) — official payload structure:
    [
      {
        "Guid": "string",
        "TestCaseId": 0,
        "TestCaseName": "string",
        "Finalize": true,
        "ValueSetName": "string",
        "TestScenarioInfo": {
          "Index": 0,
          "RunDependency": [
            {
              "RunIndex": 0,
              "OnSuccessOnly": true
            }
          ],
          "TestScenarioId": 0,
          "TestJobId": 0
        },
        "Steps": [
          {
            "Index": 0,
            "Name": "string",
            "StepType": "Condition",
            "Status": "NotRun",
            "Description": {
              "Html": "string",
              "IncompatibleRichTextFeatures": true,
              "PlainText": "string"
            },
            "ExpectedResults": {
              "Html": "string",
              "IncompatibleRichTextFeatures": true,
              "PlainText": "string"
            },
            "ActualResults": {
              "Html": "string",
              "IncompatibleRichTextFeatures": true,
              "PlainText": "string"
            },
            "ActualResultsLastUpdatedBy": {
              "Id": 0,
              "UserName": "string",
              "FirstName": "string",
              "Surname": "string",
              "Fullname": "string",
              "Email": "string",
              "Phone": "string",
              "Position": "string",
              "PictureUrl": "string"
            },
            "ActualResultsLastUpdated": {
              "Text": "string",
              "FieldValueType": "string",
              "Value": "2019-08-24T14:15:22Z"
            }
          }
        ],
        "TestedVersion": "string",
        "ExecutionDuration": {
          "Text": "string",
          "FieldValueType": "string",
          "Value": 0,
          "Unit": "Day"
        },
        "AttachedLabels": [
          {
            "Id": 0,
            "Name": "string",
            "Description": "string",
            "ParentId": 0
          }
        ],
        "CustomFields": [
          {
            "FieldId": "string",
            "Value": null
          }
        ],
        "Attachments": [
          {
            "Guid": "string"
          }
        ],
        "Status": "NotRun",
        "ShouldPropagateStatusToSteps": true
      }
    ]
- Responses: 201 Created with execution ID; 4xx for validation; 5xx transient.
- Rate limiting: 429 with Retry-After.

## 8. Architecture & Design (SOLID + DI)
- Composition Root: Program.cs initializes HostBuilder, registers services,
  parses CLI, runs ImportUseCase.
- Interfaces
  - IJUnitParser: Parses stream/file into IEnumerable<TestCaseResult>.
  - ITestCaseIdResolver: Resolves int? from a TestCaseResult or name string.
  - IAquaClient: Abstraction over HTTP to create executions; supports retries.
  - IImporter: Orchestrates: discover files -> parse -> resolve IDs -> call
    Aqua -> summarize.
  - IFileSystem: Abstraction for file IO to enable testing.
  - IClock: Provides current time for deterministic tests.
  - ILogger<T>: Use Microsoft.Extensions.Logging.
- Implementations
  - JUnitParser: LINQ to XML (System.Xml.Linq) with resilience to format
    variants.
  - RegexTestCaseIdResolver: configurable regex.
  - AquaClient: HttpClient-based; Polly for retries (or custom retry handler) —
    can implement simple retries without external dep if needed.
  - Importer: pipeline coordination.
  - FileSystem: wrapper over System.IO.
  - Clock: system clock.
- SOLID
  - SRP: each class has single responsibility.
  - OCP: new mapping strategies via new ITestCaseIdResolver implementations.
  - LSP: interfaces thin and substitutable.
  - ISP: granular interfaces; tests mock what they need.
  - DIP: high-level modules depend on abstractions; DI container composes
    concretions.

## 9. Data Models
- TestCaseResult
  - string ClassName
  - string Name
  - double? DurationSeconds
  - TestOutcome Outcome (Passed, Failed, Error, Skipped)
  - string? ErrorMessage
  - string? ErrorDetails
  - DateTimeOffset? StartedAt
  - DateTimeOffset? FinishedAt
- AquaExecutionRequest (internal model)
  - int TestCaseId
  - string Status
  - int? DurationMs
  - DateTimeOffset? StartedAt
  - DateTimeOffset? FinishedAt
  - string? ExternalRunId
  - string? RunName
  - string? ErrorMessage
  - string? ErrorDetails
  - int? ProjectId
  - Note: IAquaClient transforms AquaExecutionRequest into the official POST /api/TestExecution payload (see Section 7); the wire format follows Aqua’s schema (e.g., Guid, Steps[], ExecutionDuration, etc.).

## 10. CLI UX Examples
- Basic
  JUnitXmlImporter3.exe --input tests\results --aqua-url
  https://app.aqua-cloud.io --username %AQA_USERNAME% --password %AQA_PASSWORD% --run-name "CI #1042"
- Custom regex & dry-run
  JUnitXmlImporter3.exe -i results.xml --regex "\bCASE-(\d+)\b" --dry-run
- Strict mode
  JUnitXmlImporter3.exe -i artifacts --fail-on-unmapped --retries 5

## 11. Logging Examples
- Info: Parsed file X.xml (42 tests, 40 mapped, 2 skipped)
- Warn: Unmapped test case: "Login_TC" (classname WebTests) — skipping
- Error: Aqua API failed for TestCaseId=12345 after 3 retries: 500 Internal
  Server Error

## 12. Testing Strategy (NUnit + Shouldly)
- Unit Tests
  - RegexTestCaseIdResolver should extract IDs from various formats; reject
    invalid.
  - JUnitParser should parse junit-platform and surefire variants; map
    outcomes; handle CDATA failures.
  - Importer should skip unmapped when configured; fail when fail-on-unmapped.
  - AquaClient should build correct payload and headers; retry on 5xx and 429
    (mock HttpMessageHandler).
  - File discovery honors patterns and recursion.
- Integration Tests (optional, mocked Aqua)
  - End-to-end using an in-memory HTTP server capturing requests.
- Test Utilities
  - Sample XML fixtures for: success, failures, errors, skipped, parameterized
    tests.
- Tooling
  - NUnit for tests; Shouldly for assertions; cover edge cases; [DEBUG_LOG]
    messages where needed.

## 13. Acceptance Criteria
- Given a JUnit XML with testcases named with IDs (e.g., "Login [TC:12345]"),
  when running with valid Aqua credentials (username/password) so a token can be obtained, then one execution per mapped test is
  created in Aqua with accurate status and duration.
- When a test case lacks an ID and skip-unmapped=true, it is logged as warning
  and not sent to Aqua.
- When fail-on-unmapped=true and any test lacks ID, the process exits with code
  2 or 4 (configurable) and no API calls are made for unmapped tests.
- When --dry-run is used, no HTTP requests are sent; a summary of intended
  operations is printed.
- Errors from Aqua (5xx/429) are retried with backoff up to the configured
  limit; failures are summarized and exit code is 3 if any remain.
- DI is used throughout; core components are unit-testable in isolation.

## 14. Risks & Mitigations
- JUnit variants differ: implement tolerant parser and add fixtures for common
  formats.
- Aqua API specifics: confirm endpoint paths and required fields early; design
  IAquaClient to isolate changes.
- Rate limits: implement retries and respect rate limiting.
- Secrets leakage: mask credentials and tokens in logs and support env vars.

## 15. Open Questions
- Exact Aqua endpoints and domain object names? (Confirm in Aqua API docs.)
- Are batch execution imports supported/preferred? If yes, specify payload and
  size limits.
- Required scoping fields: projectId/custom fields?

## 16. Delivery Plan
- Milestone 1: Skeleton project, DI setup, CLI parsing, logging (2 days)
- Milestone 2: JUnit parser + unit tests (2 days)
- Milestone 3: ID resolver + unit tests (1 day)
- Milestone 4: Aqua client (mocked) + retries + tests (2 days)
- Milestone 5: Importer orchestration + dry-run + tests (2 days)
- Milestone 6: Integration test with mock server + docs (2 days)

## 17. Non-Functional Requirements
- Compatible with .NET 9; Windows/Linux runners.
- Reasonable performance: handle 10k testcases in < 2 minutes
  on typical CI agents.
- Memory: stream processing to avoid loading huge files entirely when feasible.

## 18. Maintenance & Extensibility
- Mapping strategies pluggable via DI.
- Additional exporters (future) can implement new clients without changing
  importer orchestration.

## 19. Example Mapping Cases
- "[TC:12345] Should login"
- "Checkout_TC-54321"
- "TC 42 - API health"
- Custom: "CASE-777" with regex \bCASE-(\d+)\b

## 20. Appendix: Minimal Class Skeleton (for reference)
- Interfaces
  - public interface IJUnitParser { IEnumerable<TestCaseResult> Parse(Stream s);
    }
  - public interface ITestCaseIdResolver { int? ResolveId(TestCaseResult test);
    }
  - public interface IAquaClient {
      Task<CreateExecutionResult> CreateExecutionAsync(
        AquaExecutionRequest req,
        CancellationToken ct
      );
    }
  - public interface IImporter { Task<int> RunAsync(CancellationToken ct); }
- DI Registration
  - services.AddSingleton<IJUnitParser, JUnitParser>();
  - services.AddSingleton<ITestCaseIdResolver, RegexTestCaseIdResolver>();
  - services.AddHttpClient<IAquaClient, AquaClient>();
  - services.AddSingleton<IImporter, Importer>();

End of document.

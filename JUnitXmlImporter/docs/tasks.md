# Implementation Tasks Checklist — JUnit XML Importer for Aqua Test Executions

Version: 1.0
Date: 2025-08-21

Below is an ordered, actionable checklist derived from docs/requirements.md and docs/plan.md. Each item starts with a checkbox placeholder to mark completion during implementation.

1. [ ] Confirm Aqua API endpoints and payload shape against the latest Aqua docs; record any discrepancies as TODOs in IAquaClient and docs.
2. [x] Define domain enum TestOutcome { Passed, Failed, Error, Skipped }.
3. [x] Define domain class TestCaseResult with fields: ClassName, Name, DurationSeconds?, Outcome, ErrorMessage?, ErrorDetails?, StartedAt?, FinishedAt?.
4. [x] Define internal model AquaExecutionRequest with fields: TestCaseId, Status, DurationMs?, StartedAt?, FinishedAt?, ExternalRunId?, RunName?, ErrorMessage?, ErrorDetails?, ProjectId?, WorkspaceId?.
5. [x] Create options classes: AquaOptions, MappingOptions, BehaviorOptions, HttpOptions, InputOptions, RunOptions, LoggingOptions.
6. [x] Implement configuration loading and precedence: CLI args > JSON config file > environment variables > defaults.
7. [x] Implement environment variable substitution for ${VAR} placeholders in JSON-loaded options.
8. [x] Set up DI container in Program.cs (Generic Host or minimal) and register all services and options.
9. [x] Configure Microsoft.Extensions.Logging with console provider and level control via options.
10. [x] Implement secret redaction in logs (mask username/password/token) via logging scopes/filters.
11. [x] Implement IFileSystem abstraction and FileSystem concrete wrapper over System.IO.
12. [x] Implement input discovery service: accept multiple file/dir paths; recurse directories (toggle); filter by search pattern (default *.xml); de-duplicate; log counts.
13. [x] Implement optional stdin support (stretch goal) when no paths provided; guard behind a clear option/behavior.
14. [x] Implement JUnitParser (IJUnitParser): parse surefire and junit-platform variants; extract classname, name, time, outcome; read failure/error/skipped nodes; capture messages/CDATA; capture timestamps where available.
15. [x] Ensure parser handles locale decimal separators for durations and missing duration cases gracefully.
16. [x] Implement outcome mapping in parser: failure → Failed; error → Error; skipped → Skipped; none → Passed.
17. [x] Consider streaming parsing with XmlReader for large files; fall back to XDocument when appropriate.
18. [x] Implement TestScenarioId and TestCaseId resolvers that extract IDs from the JUnit <testcase> name field using strict patterns:
    - TestScenarioId: TS followed by six digits (TS000000)
    - TestCaseId: TC followed by six digits (TC000000)
19. [x] Provide RegexStrategy defaults:
    - TestScenarioId pattern: (?<![A-Z0-9])TS([0-9]{6})(?![0-9])
    - TestCaseId pattern: (?<![A-Z0-9])TC([0-9]{6})(?![0-9])
20. [x] Validate extracted IDs are within [0, 999999]; treat leading zeros as part of the six-digit format; use numeric value for IDs.
21. [x] Implement behavior flags: skip-unmapped (warn) and fail-on-unmapped (error/exit policy) when either ScenarioId or CaseId is missing.
22. [x] Implement IAquaClient authentication: POST /api/token (grant_type=password) with form-encoded body; store bearer token securely; HTTPS only.
23. [x] Map internal AquaExecutionRequest list to official POST /api/TestExecution array payload per PRD Section 7 (Guid, Status, ExecutionDuration, etc.).
24. [x] Implement per-scenario submission: group executions by TestScenarioId and perform a separate POST /api/TestExecution for each scenario group.
25. [x] Implement retry/backoff on 5xx and 429 with exponential backoff; respect Retry-After header when present; make attempts configurable.
26. [x] Implement HttpClient timeout via options; pass CancellationToken throughout requests.
27. [x] Ensure diagnostics log request summaries (counts) and response statuses without logging secrets or full sensitive payloads.
28. [x] Implement Importer (IImporter) pipeline: discover files → parse → resolve ScenarioId and CaseId and filter per behavior → map to AquaExecutionRequest list → group by TestScenarioId → dry-run or submit per scenario → summarize results.
29. [x] Implement dry-run mode: skip network calls; output preview/counts of what would be sent; honor all filtering logic.
30. [x] Implement final summary reporting: files processed, testcases parsed, mapped, skipped, posted, failed.
31. [x] Implement exit codes: 0 success; 2 parsing/mapping issues (when not configured to fail); 3 API failures after retries; 4 configuration errors.
32. [x] Map internal TestOutcome values to Aqua Status vocabulary; confirm mapping table with Aqua docs and adjust if needed.
33. [x] Implement CLI parsing for all documented options: --input/-i, --aqua-url, --username, --password, --workspace, --project, --run-name, --regex, --skip-unmapped, --fail-on-unmapped, --dry-run, --timeout, --retries, --log-level, --config, search pattern, recursive toggle.
34. [x] Validate configuration at startup (required fields, URL format, credentials presence); emit actionable errors; return exit code 4 on invalid config.
35. [x] Ensure all log output masks credentials/tokens and redacts secrets in exception messages.
36. [x] Unit test: Scenario/Case ID extraction using strict TS/TC six-digit patterns; cover positive and negative cases.
37. [x] Unit test: Any alternative/custom resolver behavior and validation.
38. [x] Unit test: JUnitParser for surefire variant (pass/fail/error/skipped, durations, messages, CDATA).
39. [x] Unit test: JUnitParser for junit-platform variant and parameterized names.
40. [x] Unit test: Input discovery (patterns, recursion, deduplication) using IFileSystem abstraction.
41. [x] Unit test: Importer behavior with skip-unmapped=true (warnings, filtered out, exit code 0/2 as configured).
42. [x] Unit test: Importer with fail-on-unmapped=true (no API calls for unmapped; correct exit code).
43. [x] Unit test: AquaClient builds correct headers and maps payload fields per Section 7; when grouping by TestScenarioId, sends one POST per scenario (mock HttpMessageHandler).
44. [x] Unit test: Retry/backoff behavior on 5xx and 429, honoring Retry-After.
45. [x] Create XML fixtures for tests: pure success, failures with CDATA, errors, skipped, parameterized tests.
46. [ ] Optional integration test: in-memory HTTP server capturing POST /api/TestExecution to validate full payload and summarize receipt.
47. [ ] Provide example JSON config in docs with env placeholders (${AQA_USERNAME}, ${AQA_PASSWORD}).
48. [ ] Add README/usage examples, including CLI samples and CI snippets for GitHub Actions and Azure DevOps.
49. [ ] Optimize performance for large suites: use streaming where possible; validate handling ~10k testcases < 2 minutes on CI hardware.
50. [ ] Ensure memory footprint is reasonable; avoid loading entire large XML files into memory when not necessary.
51. [x] Centralize status-to-Aqua mapping; make configurable if Aqua vocabulary differs; add TODO to revisit after API confirmation.
52. [ ] Unit test: redaction utilities ensure secrets are masked in logs.
53. [x] Wire DI registrations for all services (IFileSystem, IJUnitParser, ITestCaseIdResolver, IAquaClient, IImporter, IClock) and options in Program.cs.
54. [x] Implement IClock abstraction and SystemClock implementation; use for timestamps and timing in tests.
55. [x] Implement graceful cancellation via CancellationToken across the pipeline and HTTP operations.
56. [ ] Verify CLI works per PRD examples; confirm dry-run behavior; adjust messages for clarity.
57. [ ] Document exit codes and troubleshooting guidance in docs.
58. [ ] Package app for CI consumption (dotnet publish); optionally provide single-file/self-contained instructions and multi-RID guidance.
59. [ ] Add limitations and open questions to docs; keep IAquaClient TODOs for confirmed payload details and batch size limits.
60. [ ] Add config precedence tests ensuring CLI > JSON > env > defaults.
61. [ ] Final acceptance verification against PRD Section 13; record results and any deviations.

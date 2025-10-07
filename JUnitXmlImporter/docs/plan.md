# Implementation Plan — JUnit XML Importer for Aqua Test Executions

Version: 1.0
Date: 2025-08-21
Owner: JUnitXmlImporter3 Project

This plan translates the Program Requirements Document (PRD) into concrete implementation steps and design decisions. It is organized by themes/areas of the system. Each section includes rationale to explain why the approach satisfies goals and constraints.

## 1) Summary of Goals, Scope, and Constraints
- Goals
  - Parse one or more JUnit XML result files and extract per-test outcomes.
  - Resolve Aqua Test Scenario IDs and Test Case IDs from the JUnit <testcase> name field (TS000000 and TC000000 patterns).
  - Submit test executions grouped by TestScenarioId: perform a separate POST /api/TestExecution per unique scenario, with retries and rate-limit handling.
  - Provide robust configuration (CLI > JSON > env > defaults), structured logging, and proper exit codes.
  - Use DI (IServiceCollection/IServiceProvider), apply SOLID, and maintain an automated test suite (NUnit + Shouldly).
- Non-goals
  - Not a generic test management sync; Aqua-focused only.
  - Not a full JUnit validator; assume standard outputs.
  - Do not ship tests as part of the artifact.
- Constraints
  - .NET 9, console app; Windows/Linux CI agents.
  - Security: credentials via env or secure store; mask secrets; HTTPS only.
  - Performance: up to ~10k testcases < 2 minutes; stream where feasible.

Rationale: Captures the essence of PRD sections 2, 4, 7, 17 to guide all downstream design.

## 2) Architecture & Composition Root (SOLID + DI)
- Composition Root
  - Program.cs creates a Host (Generic Host) or minimal DI container.
  - Register interfaces to implementations; bind configuration; set up logging.
  - Resolve IImporter and invoke RunAsync.
- Core Interfaces
  - IJUnitParser → parses JUnit XML into IEnumerable<TestCaseResult>.
  - ITestCaseIdResolver → maps TestCaseResult/name to int? ID.
  - IAquaClient → submits executions to Aqua, encapsulating HTTP and retries.
  - IImporter → orchestrates discovery → parse → resolve → batch post → summarize.
  - IFileSystem → file IO abstraction for testability.
  - IClock → time source for deterministic tests.
- Implementations
  - JUnitParser (System.Xml.Linq / streaming where possible).
  - RegexTestCaseIdResolver; pluggable strategies via DI.
  - AquaClient (HttpClient + retry/backoff + 429 handling; respect Retry-After).
  - Importer (pipeline coordinator with dry-run mode and summarization).
  - FileSystem (System.IO wrapper) and Clock (system clock).

Rationale: Aligns with PRD section 8 (SOLID + DI), enabling substitution and unit testing while isolating Aqua-specific concerns behind IAquaClient.

## 3) Data Models
- Domain
  - TestCaseResult
    - ClassName: string
    - Name: string
    - DurationSeconds: double?
    - Outcome: enum { Passed, Failed, Error, Skipped }
    - ErrorMessage: string?
    - ErrorDetails: string?
    - StartedAt: DateTimeOffset?
    - FinishedAt: DateTimeOffset?
  - AquaExecutionRequest (internal model used by Importer/AquaClient)
    - TestCaseId: int
    - Status: string (Aqua-compatible status mapping)
    - DurationMs: int?
    - StartedAt, FinishedAt: DateTimeOffset?
    - ExternalRunId, RunName: string?
    - ErrorMessage, ErrorDetails: string?
    - ProjectId, WorkspaceId: int?
- Wire Format
  - IAquaClient maps AquaExecutionRequest to the official POST /api/TestExecution payload (array) as documented in the PRD (Section 7).

Rationale: Keeps internal model simple and stable while isolating changes in Aqua wire schema to IAquaClient.

## 4) Configuration & CLI
- Precedence: CLI args > JSON config file > environment variables > defaults.
- CLI Options (selected)
  - --input/-i: file/dir paths (multi); recursive search toggle; pattern.
  - --aqua-url, --username, --password; workspaceId/projectId if needed.
  - --run-name; --regex; mapping strategy selection.
  - --skip-unmapped (default true); --fail-on-unmapped (default false).
  - --dry-run; --timeout; --retries; --log-level; --config path.
- Configuration Binding
  - POCO options classes: AquaOptions, MappingOptions, BehaviorOptions, HttpOptions, InputOptions, RunOptions, LoggingOptions.
  - Bind from IConfiguration (JSON + env + CLI) using Microsoft.Extensions.Configuration and CommandLine/ System.CommandLine (or simple custom parsing).
- Secrets & Env
  - Support env placeholders like ${AQA_USERNAME} in JSON; resolve at runtime.

Rationale: Matches PRD Section 5 for robustness and CI friendliness.

## 5) Input Discovery
- Accept N input paths (files/dirs). For directories, search recursively for pattern (default *.xml; configurable).
- IFileSystem implementation to list and filter files.
- Optionally accept stdin (stretch goal) when no paths provided.
- De-duplicate found files; log counts.

Rationale: Ensures flexible CI usage and deterministic behavior in tests.

## 6) JUnit Parsing
- Parser Responsibilities
  - Support common variants (Surefire, junit-platform) with tolerant reading.
  - Extract testcase elements: classname, name, time; outcome; failure/error/skipped nodes; messages and CDATA content.
  - Capture timing where available (timestamp/time attributes) and best-effort duration calculation.
- Implementation Notes
  - Use XDocument/XElement with care for large files; consider XmlReader for streaming large suites.
  - Map outcomes:
    - failure → Failed; error → Error; skipped → Skipped; none → Passed.
  - Handle special cases: parameterized names, nested suites, missing durations, locale decimal separators.
  - Unit tests: fixtures for success/failure/error/skipped, junit-platform, surefire.

Rationale: Meets PRD functional requirements 2 and performance guidance.

## 7) ID Resolution (Scenario and Case)
- Source: Extract from JUnit <testcase> name field.
- Default Regex patterns (strict):
  - TestScenarioId: (?<![A-Z0-9])TS([0-9]{6})(?![0-9])
  - TestCaseId: (?<![A-Z0-9])TC([0-9]{6})(?![0-9])
- Strategies
  - RegexStrategy (default; patterns configurable via options).
  - Extension via DI: custom resolvers are allowed.
- Validation & Behavior
  - IDs must be in [0, 999999]; leading zeros allowed (six-digit format in names).
  - Behavior on missing IDs: skip (warn), or fail based on options.

Rationale: Aligns with PRD update to fixed TS/TC + six-digit patterns and extraction from XML name field.

## 8) Aqua API Client
- Authentication
  - Obtain token via POST /api/token (grant_type=password) with form-encoded body; store Bearer token; refresh on 401 if needed (optional).
- Submission
  - Group executions by TestScenarioId. For each group, build an array payload and POST to /api/TestExecution. In dry-run, do not call network—serialize per-scenario previews to logs.
- Retries & Rate Limiting
  - Exponential backoff for 5xx/429; respect Retry-After header; configurable max attempts and base delay.
  - Circuit simple: no external libs required; optionally use Polly if allowed.
- Timeouts
  - HttpClient timeout configurable; per-request CancellationToken.
- Mapping
  - Map internal outcomes to Aqua Status values and duration to Aqua’s ExecutionDuration structure per PRD example.
- Diagnostics
  - Log request summary (counts) and response statuses; never log secrets or full payload with secrets.

Rationale: Encapsulates Aqua specifics; adheres to PRD section 4 and 7 with per-scenario POST arrays and robust HTTP behavior.

## 9) Import Orchestration (IImporter)
- Pipeline
  1) Discover files from inputs (IFileSystem).
  2) For each file: parse testcases (IJUnitParser).
  3) Resolve IDs (ITestCaseIdResolver) and filter per behavior (skip/fail).
  4) Map to AquaExecutionRequest list.
  5) If dry-run: output summary and return appropriate exit code.
  6) Else: group by TestScenarioId and call IAquaClient for each scenario group; handle retries.
  7) Summarize results and return exit code based on errors/unmapped policy.
- Metrics
  - Track files processed, cases parsed, mapped, skipped, posted, failed.

Rationale: Centralizes flow control and supports deterministic summary and exit codes.

## 10) Logging & Telemetry
- Use Microsoft.Extensions.Logging with structured logs.
- Include per-test context (class, name, resolvedId) where useful.
- Final summary: totals and exit reasoning.
- Log levels per options; mask tokens and passwords.

Rationale: Meets PRD logging requirements and aids CI troubleshooting.

## 11) Error Handling & Exit Codes
- Categories: parsing, mapping, API, configuration.
- Exit Codes
  - 0: success.
  - 2: parsing/mapping issues but completed (when not configured to fail).
  - 3: API failures after retries.
  - 4: configuration errors (missing credentials/URL, invalid options).
- Behavior flags control whether unmapped IDs cause warnings or failure.

Rationale: Aligns with PRD section 6.7 and acceptance criteria.

## 12) Security
- Credential handling via env or secure input; never log secrets.
- HTTPS only; rely on system TLS validation.
- Mask credentials and tokens in logs.

Rationale: Satisfies PRD security constraints.

## 13) Performance & Scalability
- Stream files when large; avoid loading entire files when not necessary.
- Minimize allocations; reuse buffers if using XmlReader.
- Batch per-scenario POSTs (one per TestScenarioId) to align with API requirements; consider optional concurrency with rate-limit awareness.

Rationale: Meets NFRs and reduces CI time.

## 14) Testing Strategy (NUnit + Shouldly)
- Unit Tests
  - Scenario/Case ID resolvers (strict TS/TC six-digit patterns) edge cases and invalid inputs.
  - JUnitParser across variants; outcome mapping; CDATA extraction.
  - Importer behavior for skip vs fail-on-unmapped; dry-run logic, per-scenario grouping.
  - AquaClient payload composition, headers, retries/backoff (mock HttpMessageHandler); verify one POST per scenario.
  - File discovery respects pattern and recursion.
- Integration (optional)
  - In-memory HTTP server to capture Aqua payloads and assert structure.
- Test Utilities
  - XML fixtures for pass/fail/error/skipped/parameterized tests.
- Tooling
  - NUnit and Shouldly; add [DEBUG_LOG] messages as needed.

Rationale: Ensures correctness and guards regressions per PRD section 12.

## 15) Developer Experience & Packaging
- Build: .NET 9; multi-RID self-contained publish optional.
- App configuration via appsettings.json (optional) + custom JSON config path.
- Provide example config file in docs.
- Packaging: dotnet publish to produce single-file exe (optional) for CI integration.

Rationale: Smooth CI/DevOps adoption.

## 16) CI/CD Considerations
- Pipeline Steps
  - Restore, build, test (run unit tests), publish artifacts.
  - Lint/format optional.
- Secrets
  - Inject AQA_USERNAME/AQA_PASSWORD via secure secrets store.
- Example Usage in CI docs for GitHub Actions/Azure DevOps.

Rationale: Facilitates adoption by stakeholders (QA/DevOps).

## 17) Risks & Mitigations
- JUnit format variability → tolerant parser + comprehensive fixtures.
- Aqua API shape changes → isolate mapping in IAquaClient; add contract tests against recorded examples.
- Rate limits/transient errors → retries with backoff; honor Retry-After; summarize failures.
- Secret leakage → centralized logging filters; redaction utilities.

## 18) Open Questions & Assumptions
- Confirm exact Aqua endpoints/required fields and allowable batch sizes.
- Confirm status mapping vocabulary expected by Aqua.
- Clarify workspace/project scoping requirements.

Action: Track as TODOs in IAquaClient and update upon confirmation.

## 19) Milestones & Timeline (from PRD)
- M1 (2 days): Project skeleton, DI setup, CLI parsing, logging.
- M2 (2 days): JUnit parser + unit tests.
- M3 (1 day): ID resolver + unit tests.
- M4 (2 days): Aqua client (mocked) + retries + tests.
- M5 (2 days): Importer orchestration + dry-run + tests.
- M6 (2 days): Integration test with mock server + docs.

## 20) Acceptance Criteria Traceability
- Mapped to PRD Section 13:
  - Executions created per mapped test with accurate status/duration when credentials valid.
  - Unmapped handling per flags; correct exit codes.
  - Dry-run produces summary only.
  - Retries/backoff honored; failures summarized with exit code 3.
  - DI across components and unit-testability ensured.

## 21) Implementation Task Breakdown
- Project Setup
  - Add Options classes and configuration binding.
  - Wire DI in Program.cs; configure logging.
- Files & Discovery
  - Implement IFileSystem and discovery service.
- Parsing
  - Implement JUnitParser with tests and fixtures.
- ID Resolution
  - Implement RegexTestCaseIdResolver + alt strategy interface.
- Aqua Client
  - Implement token acquisition and POST array payload; retries/backoff.
  - Outcome/status mapping; duration conversion.
- Orchestrator
  - Implement Importer pipeline, dry-run mode, summarization, exit codes.
- Logging & Security
  - Structured logs; secret redaction; log level control.
- Tests
  - Unit tests per components; optional integration test with mock server.
- Docs
  - Usage examples, config sample, limitations.

## 22) Done Definition
- All unit tests pass; optional integration test validates payload.
- CLI works per examples; dry-run behaves correctly.
- Handles 10k testcases within performance targets on CI-like hardware.
- Code structured with DI, SOLID; secrets masked; error codes correct.

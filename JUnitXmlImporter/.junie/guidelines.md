# C# Style Guide (Modern, Common‑Sense Defaults)

This repository targets .NET 9 and modern C# (C# 12+). The goal of this guide is to keep code readable, consistent, and safe, without being dogmatic. When in doubt, favor clarity over cleverness.

Scope: applies to all production code in this repo. For prototypes or spikes, follow as reasonable and clean up before merging.

## 1. Language and Project Settings
- Nullable reference types: enabled. Use nullability annotations intentionally.
- Treat warnings seriously. Fix new analyzer warnings promptly; do not broadly suppress.
- Use file‑scoped namespaces.
- Prefer latest stable C# language version supported by the target framework.

## 2. Naming Conventions
- Namespaces: PascalCase, company/product first, then feature areas (e.g., Company.Product.Feature).
- Types (classes, structs, records, enums, delegates): PascalCase.
- Interfaces: PascalCase prefixed with I (e.g., ILogger).
- Methods, Properties, Events: PascalCase.
- Parameters and local variables: camelCase.
- Private/protected fields: _camelCase (leading underscore). Example: private readonly ILogger _logger;
- Public fields (rare): PascalCase.
- Constants: PascalCase (e.g., DefaultTimeoutMs).
- Enum members: PascalCase.
- Acronyms: treat as words (HttpClient, not HTTPClient).
- File names: one public type per file, file name matches type (TypeName.cs). Partial types: include a suffix (TypeName.Part.cs when helpful).

## 3. Layout and Formatting
- Indentation: 4 spaces. No tabs.
- Line length: aim for 120 characters max; wrap when it improves readability.
- Braces: Allman style (opening brace on a new line).
- Use expression‑bodied members when they remain readable and short.
- Place using directives at the top of the file, outside the namespace (with file‑scoped namespaces this is natural). System namespaces first, then others. Remove unused usings.
- One statement per line. Avoid multiple declarations per line.
- Blank lines: group related code logically; avoid excessive vertical whitespace.

## 4. Language Features
- var usage:
  - Use var when the type is obvious from the right side or not important to the reader.
  - Otherwise, spell out the type for clarity.
- Target‑typed new: use when it clearly improves readability, otherwise keep explicit types.
- Pattern matching and switch expressions: prefer when it simplifies code and remains readable.
- Null handling: prefer ?. and ??; use is not null for clarity. Avoid double‑checked null patterns unless necessary.
- Strings: prefer interpolation ($"...") over concatenation. Use string.Create or Span<char> in perf‑critical paths only when needed.
- Records:
  - Use record or record struct for pure data carriers with value semantics.
  - Use init‑only setters for immutable models; favor immutability where practical.
- Collections and LINQ:
  - Prefer foreach for side‑effects; LINQ for transformations and queries.
  - Avoid multiple enumeration (call ToList/ToArray only when you explicitly need materialization).
  - Be mindful of allocations in hot paths.

## 5. Async and Concurrency
- Suffix asynchronous methods with Async (GetDataAsync).
- Do not use async void except for event handlers.
- Prefer CancellationToken as the last optional parameter when operations can be canceled.
- In libraries that might run under a synchronization context, consider ConfigureAwait(false) for library internals. In console/worker apps, it’s typically unnecessary.
- Avoid blocking on async code (no .Result or .Wait()).

## 6. Exceptions and Guard Clauses
- Throw the most specific exception type appropriate.
- Guard clauses at method entry for argument validation.
  - Use ArgumentNullException.ThrowIfNull(param) for null checks.
  - Use ArgumentOutOfRangeException for range violations. Provide nameof(param) and a helpful message.
- Do not swallow exceptions. Catch only when you can add context or handle/retry.
- Avoid using exceptions for control flow.

## 7. APIs and Visibility
- Keep members as private as possible. Expose only what is necessary.
- Prefer small, focused types and methods. Single‑responsibility where feasible.
- Use interface abstractions for external dependencies to enable testing.

## 8. Documentation and Comments
- Public APIs: add XML documentation summaries where intent isn’t obvious.
- Write comments to explain why, not what. Keep code self‑explanatory when possible.
- Use TODO: and FIX: annotations sparingly and with context. Link to issue IDs when available.

## 9. Testing (if applicable)
- Name tests clearly using Behavior_ExpectedOutcome pattern or ArrangeActAssert sections.
- Avoid hidden test coupling. Keep tests deterministic.

## 10. Code Ordering Inside Types
Recommended order (flexible; keep consistent within a file):
1. Constants
2. Fields
3. Constructors
4. Properties/Indexers
5. Events
6. Public methods
7. Internal methods
8. Protected methods
9. Private methods
10. Nested types

## 11. Performance Considerations
- Measure before optimizing. Use BenchmarkDotNet for micro‑benchmarks if needed.
- Avoid unnecessary allocations in tight loops and hot paths.
- Prefer Span/Memory in low‑level code only when justified by profiling.

## 12. Analyzers and Tooling
- Enable .NET analyzers (Microsoft.CodeAnalysis.NetAnalyzers). Address warnings.
- Consider StyleCop or similar only if it adds clear value; don’t let style tools hinder delivery.
- Keep .editorconfig (if/when added) aligned with this guide. Prefer the SDK defaults with minimal overrides.

### Suggested .editorconfig snippet (optional)
These align with the guidance above. Add/adjust only if needed.

```editorconfig
[*.cs]
dotnet_style_namespace_match_folder = true:suggestion
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_new_line_before_open_brace = all
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion
csharp_style_prefer_switch_expression = true:suggestion
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_prefer_null_check_over_type_check = true:suggestion
csharp_style_prefer_pattern_matching = true:suggestion
csharp_prefer_simple_default_expression = true:suggestion

# Naming
dotnet_naming_rule.private_fields_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_underscore.style = underscore_prefix
dotnet_naming_rule.private_fields_underscore.severity = suggestion

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private, protected, protected_internal

dotnet_naming_style.underscore_prefix.capitalization = camel_case
dotnet_naming_style.underscore_prefix.required_prefix = _
```

## 13. Pull Requests and Reviews
- Small, focused PRs. Include context in the description.
- Run and fix analyzers/warnings before review.
- Keep the codebase consistent with this guide; propose changes when it makes sense.

---
This guide is intentionally concise. When unsure, prefer readability, maintainability, and the principle of least surprise.

---
paths:
  - "src/RoslynTestKit/*Fixture*.cs"
  - "src/RoslynTestKit/CodeMarkup.cs"
  - "src/RoslynTestKit/Verify.cs"
---

# Fixture wiring

## Option flow

Each config property follows: `BaseTestFixtureConfig` property -> `BaseTestFixture` `protected virtual` -> override in all four `Configurable*` files -> consumer site.

| Config property | Fixture virtual | Consumer site |
|---|---|---|
| `References` | `References` | `CreateDocumentFromCode` (extra metadata refs) |
| `ThrowsWhenInputDocumentContainsError` | `ThrowsWhenInputDocumentContainsError` | `AnalyzerTestFixture.GetDiagnostics`, `CodeFixTestFixture.GetReportedDiagnostics` |
| `Language` | `LanguageName` | `CreateDocumentFromCode` |
| `AdditionalFiles` | `AdditionalFiles` | `AnalyzerTestFixture.GetDiagnostics`, `CodeFixTestFixture.GetAllReportedDiagnostics` and `GetProjectDiagnosticsLikeHost` (AnalyzerOptions) |
| `RuleSetPath` | `RuleSetPath` | `BaseTestFixture.ApplyRuleSet` and `ProjectSettings` |
| `PackageCachePaths` | `PackageCachePaths` | `ProjectSettings` -> `AdhocWorkspace.AddProject` |
| `CompilationOptions` | `CustomCompilationOptions` | `CreateDocumentFromCode` via `.WithCompilationOptions()` |
| `ParseOptions` | `ParseOptions` | `ProjectSettings` -> `AdhocWorkspace.AddProject` |
| `ProjectInfoCustomizer` | `ProjectInfoCustomizer` | `ProjectSettings` -> `AdhocWorkspace.AddProject` (applied after `ProjectInfo.Create`) |
| `FileSystem` | `FileSystem` | `AnalyzerTestFixture.GetDiagnostics`, `CodeFixTestFixture.GetAllReportedDiagnostics` and `GetProjectDiagnosticsLikeHost` via `compilation.WithFileSystem()` |
| `DependencyProjects` | `DependencyProjects` | `CreateDocumentFromCode` (extra workspace projects + project references from `TestProject`) |
| `AdditionalAnalyzers` (CodeFix only) | `CreateAdditionalAnalyzers` | `CodeFixTestFixture.GetAllReportedDiagnostics` and `GetProjectDiagnosticsLikeHost` (filtered to analyzers supporting the fixed ids) |

## Quirks to preserve or change deliberately

- `ThrowsWhenInputDocumentContainsError` defaults differ: config `true`, fixture `false`.
- `CodeRefactoringTestFixture.FailWhenInputContainsErrors` is not config-wired; it is a standalone `protected virtual` defaulting to `true`.
- `CodeFixTestFixture` without `AdditionalAnalyzers` falls back to `semanticModel.GetDiagnostics()` (compiler diagnostics only).
- `TestFixAll` requires marker count == diagnostic count; mismatch throws `RoslynTestKitException`.
- `TestFixAll` at Project/Workspace scope uses `EagerProjectDiagnosticProvider` (whole-compilation re-run on every provider call, like the editor's eager provider; markers counted across all `/*EOD*/` segments); Document scope keeps the pre-computed list in `TestDiagnosticProvider`. The eager provider must compute from the compilation of the `Project` it is handed: `Solution.GetDocument(SyntaxTree)` is identity-based, and in-memory documents have no `FilePath` to filter by.
- `TestFixAll` verifies via host text-edit replay (diff against the original snapshot, edits of all operations applied in one batch, like the editor's `RunCodeActionCommandHandler`) and then via workspace apply; `FixAllHostVerification.None` skips the replay. `Verify.CodeAction` without the parameter (used by `TestCodeFix`) keeps workspace apply only.
- Each dependency project gets a `ProjectDefinition` (non-empty `AppId`) through its `ProjectInfo`: the SDK's project reference manager keys the referenced module by that definition and throws a `NullReferenceException` when it is null. Dependency projects reuse the test project's settings except `ProjectInfoCustomizer`. With no dependency projects the workspace setup is the single-project path.

## Markup and multi-document tests

`CodeMarkup` strips `[|` and `|]` markers (4 chars per pair). Multiple markers are supported; `TextSpan` offsets subtract `4 * priorMarkerCount` to account for removed characters.

`/*EOD*/` splits a code string into multiple documents. The segments are reversed: the last segment becomes `TestDocument0`, while the first segment is returned as the main document (assertions run against it). `Verify` re-merges via `OrderByDescending(Name)` and normalizes CRLF to LF on both sides (PR #19).

## FileSystem injection

Purpose: analyzers that depend on `Compilation.FileSystem` (e.g. XLIFF readers) need a virtual file system in tests. The `FileSystem` property on `BaseTestFixtureConfig` accepts `IFileSystem?`. When non-null, `compilation.WithFileSystem(FileSystem)` is called before creating `CompilationWithAnalyzers`.

Flow: config property -> all four `Configurable*` overrides -> consumed in `AnalyzerTestFixture.GetDiagnostics` and `CodeFixTestFixture.GetAllReportedDiagnostics`.

The SDK's `MemoryFileSystem` class accepts `IDictionary<string, byte[]>`. `GetDirectoryPath()` returns `""`. Keys should use forward slashes. The consumer-side `ManifestHelper.GetManifest()` may throw `FileNotFoundException` in test contexts; that is a consumer issue, not a RoslynTestKit bug.

## Reproducing a consumer-reported bug without a test project

Pack locally with a `-local.N` version suffix, point `../Analyzers` `Directory.Packages.props` at it with an additional restore source, run one filtered test, then revert.

## Checklist: adding a config option

1. **Decide layer:** shared -> `BaseTestFixtureConfig`; fixture-specific -> that `*Config` + one `Configurable*`.
2. **Decide consumer site:** compilation-level in `GetDiagnostics`/`GetAllReportedDiagnostics`; project-level via `ProjectSettings` + both `AdhocWorkspace` branches; or `CreateDocumentFromCode`.
3. **Confirm** the type exists on the 12.0 DevTools (netstandard2.1 build).
4. Add config property with XML doc and sensible default.
5. Add `BaseTestFixture` `protected virtual` property.
6. Override in **all four** `Configurable*.cs` files.
7. Wire at the consumer site.
8. Add a README row under `#### Shared options` (or the fixture-specific extras table).
9. Build all TFMs + pack.
10. **Self-check:** exactly four `Configurable*.cs` files contain the override.

**Common mistakes:** wiring three of four fixtures; adding to `ProjectSettings` without the reflection `switch` case in `AdhocWorkspace` (silently uses SDK default on net8.0+); forgetting the README row; using a Roslyn type instead of the Nav equivalent.

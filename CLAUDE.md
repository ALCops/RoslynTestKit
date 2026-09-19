# RoslynTestKit

## What this is

NuGet library (`ALCops.RoslynTestKit`) for unit-testing AL analyzers, code fixes, refactorings
and completion providers. Fork of cezarypiatek/RoslynTestKit with `Microsoft.CodeAnalysis` types
swapped for `Microsoft.Dynamics.Nav.CodeAnalysis`. Single project, namespace `RoslynTestKit`.
No test project; the library is the deliverable. GitHub issues are disabled; `#N` in git
history refers to a PR number.

## Build, pack, verify

```
dotnet restore src/RoslynTestKit.sln
dotnet pack src/RoslynTestKit/RoslynTestKit.csproj -c Release -o ./artifacts
```

Verification for any change: Release pack of all three TFMs (`netstandard2.1`, `net8.0`,
`net10.0`) succeeds and `artifacts/*.nupkg` contains `lib/netstandard2.1/RoslynTestKit.dll`.
Optional validation: `dotnet tool install -g dotnet-validate --version 0.0.1-preview.537`
then `dotnet-validate package local ./artifacts/*.nupkg`.

## BC DevTools local setup

Each TFM references `Microsoft.Dynamics.Nav.CodeAnalysis.dll` and `.Workspaces.dll` from
gitignored folders under `Microsoft.Dynamics.BusinessCentral.Development.Tools/` at repo root
(`HintPath`, `SpecificVersion=False`, `Private=False`).

| TFM | Folder | CI marketplace version | Csproj AssemblyFileVersion |
|---|---|---|---|
| netstandard2.1 | netstandard2.0 | 12.0.779795 | 12.0.11.58921 |
| net8.0 | net8.0 | 16.0.1463980 | 16.0.22.22232 |
| net10.0 | net10.0 | 17.0.2273547 | (not yet recorded) |

Note the deliberate netstandard2.1 TFM to netstandard2.0 folder mismatch. To fetch DLLs
locally, run the scripts in `.github/actions/setup-bc-devtools/` (`Marketplace.ps1`,
`Download-BcDevToolsAsset.ps1`, `Extract-RequiredFiles.ps1`). Local DLLs may differ from CI;
API-surface drift only surfaces in CI builds.

## Architecture

Nav types shadow `Microsoft.CodeAnalysis` via `using` aliases throughout the codebase. Always
use Nav types unless no Nav equivalent exists (e.g. `MetadataReference`).

**Fixture tree:** `BaseTestFixture` (abstract) with children `AnalyzerTestFixture`,
`CodeFixTestFixture`, `CodeRefactoringTestFixture`, `CompletionProviderFixture`. Each has an
`internal Configurable*` counterpart bridging a config object to the fixture's `protected
virtual` properties. Entry point: `RoslynFixtureFactory.Create<T>()`.

**Config:** `BaseTestFixtureConfig` exposes: `References`, `ThrowsWhenInputDocumentContainsError`,
`Language`, `AdditionalFiles`, `RuleSetPath`, `PackageCachePaths`, `CompilationOptions`,
`ParseOptions`, `ProjectInfoCustomizer`, `FileSystem`. `CodeFixTestFixtureConfig` adds
`AdditionalAnalyzers`.

**Markup:** `[|...|]` markers denote diagnostic spans (4-char offset math in `CodeMarkup`).
`/*EOD*/` separates multi-document tests; the split is reversed so the last segment becomes
`TestDocument0` while the first segment is returned as the main document.

**AdhocWorkspace:** `#if NETSTANDARD2_1` calls `ProjectInfo.Create` directly; `#if
NET8_0_OR_GREATER` uses reflection by parameter name to handle SDK signature drift between
DevTools versions.

**NavCodeAnalysisBase:** NUnit base class reading `AssemblyFileVersionAttribute` (not
marketplace version) for conditional test skipping.

**Failure output:** DiffPlex inline diffs; ApprovalTests launches external diff tool when a
debugger is attached.

## Hard constraints

- **Public API:** abstract fixtures, `RoslynFixtureFactory`, `*TestFixtureConfig` classes,
  `NavCodeAnalysisBase`, `ReferenceSource`. **Internal:** `Configurable*`, `ProjectSettings`,
  `TestCompletionService`, `TestDiagnosticProvider`.
- Everything must compile on netstandard2.1 against the 12.0 DevTools.
- Use Nav types, not Roslyn types.
- New options must be wired in all four `Configurable*` fixtures; `ProjectInfo`-level options
  also need both `AdhocWorkspace` branches (static call and reflection switch).
- No new mutable static state; one `AdhocWorkspace` per `CreateDocumentFromCode` call.

## Downstream consumer

Analyzers pins `ALCops.RoslynTestKit` via CPM and references
`lib/netstandard2.1/RoslynTestKit.dll` directly on legacy TFMs. The netstandard2.1 asset must
always ship and its public surface must not drift from the modern TFMs.

## Conventions

- README is the user-facing source of truth. Option tables (`#### Shared options` /
  `#### CodeFixTestFixtureConfig extras`) and TFM badge must update in the same PR as any
  public API or TFM change.
- `**/*.md` is excluded from CI path filters; pair README edits with code changes.
- Releases: GitVersion Mainline on push to master. Never hand-edit versions.

## Where to look

- `.claude/rules/fixture-wiring.md` -- option flow, markup, FileSystem, add-an-option checklist
- `.claude/rules/workspace-compat.md` -- TFM compat, reflection, thread safety
- `.claude/rules/devtools-tfm.md` -- DevTools versions, CI mechanics, bump-DevTools checklist
- `REVIEW.md` -- correctness checks for `/code-review`

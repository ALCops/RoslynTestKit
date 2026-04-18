# Copilot Instructions for RoslynTestKit

## What This Is

A NuGet library (`ALCops.RoslynTestKit`) for unit-testing Roslyn-based diagnostic analyzers, code fixes, refactorings, and completion providers targeting the **AL Language** (Microsoft Dynamics 365 Business Central). Forked from [cezarypiatek/RoslynTestKit](https://github.com/cezarypiatek/RoslynTestKit), it replaces `Microsoft.CodeAnalysis` types with their `Microsoft.Dynamics.Nav.CodeAnalysis` equivalents.

## Build

```bash
dotnet restore src/RoslynTestKit.sln
dotnet build src/RoslynTestKit.sln --configuration Release
dotnet pack src/RoslynTestKit/RoslynTestKit.csproj --configuration Release --output ./artifacts
```

There is no test project in this repository. The library itself is the deliverable.

The project multi-targets `netstandard2.1` and `net8.0`. Each TFM references a different version of the BC DevTools DLLs (`Microsoft.Dynamics.Nav.CodeAnalysis.dll` and `Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.dll`) from a local path. CI downloads these via the custom `.github/actions/setup-bc-devtools` composite action.

## Architecture

### Namespace aliasing pattern

Throughout the codebase, `Microsoft.Dynamics.Nav.CodeAnalysis` types shadow their `Microsoft.CodeAnalysis` counterparts via `using` aliases. This is intentional and central to the design:

```csharp
using AdditionalText = Microsoft.Dynamics.Nav.CodeAnalysis.AdditionalText;
using CompilationOptions = Microsoft.Dynamics.Nav.CodeAnalysis.CompilationOptions;
using Document = Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Document;
using LanguageNames = Microsoft.Dynamics.Nav.CodeAnalysis.LanguageNames;
using ParseOptions = Microsoft.Dynamics.Nav.CodeAnalysis.ParseOptions;
```

When adding or modifying code, always use the Nav.CodeAnalysis types, not the Microsoft.CodeAnalysis originals, unless there is no Nav equivalent (e.g., `MetadataReference`).

### Fixture hierarchy

```
BaseTestFixture (abstract, creates AdhocWorkspace + Document from code)
├── AnalyzerTestFixture (abstract, HasDiagnostic / NoDiagnostic assertions)
├── CodeFixTestFixture (abstract, TestCodeFix / NoCodeFix assertions)
├── CodeRefactoringTestFixture (abstract, TestCodeRefactoring assertions)
└── CompletionProviderFixture (abstract, completion assertions)
```

Each abstract fixture has a **Configurable** counterpart (`ConfigurableAnalyzerTestFixture`, etc.) that is `internal` and bridges a config object to the fixture's `protected virtual` properties. Users never instantiate these directly.

### Factory entry point

`RoslynFixtureFactory.Create<T>()` is the only public API consumers use. It instantiates the analyzer/provider via `new T()`, wraps it in the matching Configurable fixture, and returns the abstract base type. All overloads accept an optional config object (`AnalyzerTestFixtureConfig`, `CodeFixTestFixtureConfig`, etc.) that inherits from `BaseTestFixtureConfig`.

### Config hierarchy

```
BaseTestFixtureConfig (Language, References, AdditionalFiles, RuleSetPath, PackageCachePaths, CompilationOptions, ParseOptions, ProjectInfoCustomizer)
├── AnalyzerTestFixtureConfig
├── CodeFixTestFixtureConfig (+AdditionalAnalyzers)
├── CodeRefactoringTestFixtureConfig
└── CompletionProviderTestFixtureConfig
```

### Code markup for test spans

Test input uses `[|` and `|]` markers to denote diagnostic/refactoring spans. `CodeMarkup` parses these, strips the markers from the code, and produces `IDiagnosticLocator` instances. Multiple markers per file are supported via `AllLocators`. Prefer `HasDiagnosticAtAllMarkers` / `NoDiagnosticAtAllMarkers` over single-marker methods.

### Multi-document tests

Use the `/*EOD*/` separator in a single code string to define multiple documents in one test project. The last segment becomes the "main" document.

### NavCodeAnalysisBase

An NUnit base class for test projects consuming this library. It detects the loaded `Microsoft.Dynamics.Nav.CodeAnalysis` assembly version at runtime and provides helpers (`RequireMinimumVersion`, `SkipTestIfVersionIsTooLow`, etc.) to conditionally skip tests based on AL DevTools version.

## Conventions

- **Nullable reference types** are enabled (`<Nullable>enable</Nullable>`).
- The single namespace is `RoslynTestKit` for all public types. Helper/utility types also live in `RoslynTestKit` or `RoslynTestKit.Utils` / `RoslynTestKit.CodeActionLocators`.
- `Configurable*` fixture classes are `internal`. Only the abstract base fixtures and `RoslynFixtureFactory` are public API.
- `ProjectSettings` is `internal` and carries config values from the fixture down to `AdhocWorkspace.AddProject`.
- Code comparison on failure uses DiffPlex for inline diffs and ApprovalTests for external diff tool launch (when a debugger is attached).
- Versioning uses [GitVersion](https://gitversion.net/) in Mainline mode. Version is derived from git history, not manually maintained.

## CI/CD

- **Pull requests**: build + pack + validate (via `dotnet-validate`). Skips draft PRs.
- **Main/master push**: build + pack + validate + create GitHub Release + publish to GitHub Packages + publish to NuGet.org.
- BC DevTools DLLs are downloaded in CI from the VS Code Marketplace via the custom `setup-bc-devtools` action. Two versions are fetched: `12.0.779795` (netstandard2.0) and `16.0.1463980` (net8.0).

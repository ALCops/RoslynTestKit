---
paths:
  - "src/RoslynTestKit/Helpers/**"
  - "src/RoslynTestKit/ReferenceSource.cs"
  - "src/RoslynTestKit/CodeActionLocators/NestedCodeActionHelper.cs"
---

# Workspace and TFM compatibility

## Why AdhocWorkspace.AddProject is split per TFM

The `ProjectInfo.Create` method signature changed between DevTools versions (specifically between v17.0.28.6483 and v17.0.28.26016 of `Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.dll`). Binding it statically on netstandard2.1 means `MissingMethodException` when loaded against a newer Nav DLL with a different signature. The solution:

- `#if NETSTANDARD2_1`: calls `ProjectInfo.Create` directly with named arguments (`packageCachePaths`, `parseOptions`, `ruleSetPath`).
- `#if NET8_0_OR_GREATER`: discovers `ProjectInfo.Create` via reflection, caches the `MethodInfo` and its parameters in a `Lazy<>`, then builds the argument array by matching parameter names (lower-cased) to `ProjectSettings` properties.

## Adding a curated ProjectInfo setting

To expose a new `ProjectInfo` property through the config:

1. Add a property on `ProjectSettings`.
2. Add a lower-cased parameter-name case to the `switch` in `AdhocWorkspace.GetParameterValue` (net8.0+ reflection path).
3. Add the corresponding named argument in the netstandard2.1 `ProjectInfo.Create` call.
4. Wire it through `BaseTestFixtureConfig` -> `BaseTestFixture` -> all four `Configurable*` files -> `CreateDocumentFromCode` (where the `ProjectSettings` object is built).

Missing the reflection `switch` case means the parameter silently gets its SDK default on net8.0+.

## Thread-safety inventory

| Shared state | Safety mechanism |
|---|---|
| `AdhocWorkspace._projectInfoCreateMethod` | `Lazy<>(LazyThreadSafetyMode.PublicationOnly)` — immutable once computed |
| `ReferenceSource` statics (`Core`, `Linq`, etc.) | Static readonly fields, initialized once in static constructor |
| `ReferenceSource.NetStandardBasicLibs` | `Lazy<>` default thread safety |
| `NavCodeAnalysisBase._navCodeAnalysisVersion` | Written once in `[OneTimeSetUp]`; NUnit guarantees single-threaded setup |
| `NavCodeAnalysisBase._navCodeAnalysisAssembly` | Static readonly, initialized at class load |

No mutable static state exists outside these. Each test gets its own `AdhocWorkspace` instance from `CreateDocumentFromCode`.

## ReferenceSource

Derives metadata references from `TRUSTED_PLATFORM_ASSEMBLIES` (the runtime's app context data). Provides `Core`, `Linq`, `LinqExpression`, `NetStandardCore`, and `NetStandard2_0` as static references. `NetStandardBasicLibs` lazily loads mscorlib's referenced assemblies.

## NavCodeAnalysisBase

Reads `AssemblyFileVersionAttribute` from the loaded `Microsoft.Dynamics.Nav.CodeAnalysis` assembly — this is the four-part file version (e.g. `16.0.22.22232`), not the marketplace version string (e.g. `16.0.1463980`). Version helpers (`RequireMinimumVersion`, `SkipTestIfVersionIsTooLow`, etc.) use NUnit's `Assert.Ignore` to skip tests.

## NestedCodeActionHelper

Reflects on the non-public `NestedCodeActions` property of `CodeActionWithNestedActions`. Falls back gracefully (returns `null`) when the property is not found — must never throw.

---
applyTo: 'src/RoslynTestKit/**'
---

# FileSystem Support in RoslynTestKit

## Purpose

Some AL analyzers depend on `Compilation.FileSystem` to read workspace files at analysis time (e.g. XLIFF translation files, configuration files). The `FileSystem` config property lets tests inject a virtual file system into the compilation so these analyzers can run without touching disk.

## How it works

1. `BaseTestFixtureConfig` exposes `IFileSystem? FileSystem` (default: `null`).
2. All `Configurable*` fixture classes wire this property from the config to the base fixture.
3. `AnalyzerTestFixture.GetDiagnostics()` and `CodeFixTestFixture.GetAllReportedDiagnostics()` call `compilation.WithFileSystem(FileSystem)` before creating `CompilationWithAnalyzers`, replacing any existing file system on the compilation.
4. When `FileSystem` is `null`, no replacement occurs and the compilation retains its default (typically `null`).

## SDK's MemoryFileSystem

The `Microsoft.Dynamics.Nav.CodeAnalysis` SDK includes a public `MemoryFileSystem` class:

```csharp
public class MemoryFileSystem : IFileSystem
{
    public MemoryFileSystem(IDictionary<string, byte[]> files);
    public string GetDirectoryPath(); // always returns ""
    public IEnumerable<string> GetFiles(string glob);
    public Stream OpenRead(string path);
}
```

Key behaviors:
- Constructor uses `PackagePathComparer.Instance` internally (case-insensitive on Windows).
- `GetDirectoryPath()` returns `""` (empty string). Analyzers that use this for path resolution get an empty workspace path.
- `GetFiles(string glob)` supports single-parameter glob matching (e.g. `Translations/*.xlf`). Do NOT use the two-parameter overload as it may not behave the same way.
- Keys should use forward slashes: `Translations/MyApp.da-DK.xlf`.

## Usage pattern in tests

```csharp
var xliffContent = """
<?xml version="1.0" encoding="utf-8"?>
<xliff version="1.2">
  <file datatype="xml" source-language="en-US" target-language="da-DK">
    <body />
  </file>
</xliff>
""";

var files = new Dictionary<string, byte[]>
{
    ["Translations/TestApp.da-DK.xlf"] = Encoding.UTF8.GetBytes(xliffContent)
};

var fixture = RoslynFixtureFactory.Create<MyAnalyzer>(
    new AnalyzerTestFixtureConfig
    {
        FileSystem = new MemoryFileSystem(files)
    });
```

## Design decisions

| Decision | Choice | Rationale |
|---|---|---|
| Property location | `BaseTestFixtureConfig` | All fixture types (analyzer, codefix, refactoring, completion) may need FileSystem |
| Null default | `null` | Backward compatible; only analyzers that need FileSystem will use it |
| Injection point | Before `CompilationWithAnalyzers` creation | FileSystem must be set before analyzer callbacks fire |
| No custom MemoryFileSystem | Use SDK's built-in class | Avoids maintenance burden; SDK class is public and stable |

## Affected files

- `BaseTestFixtureConfig.cs`: `IFileSystem? FileSystem` property declaration
- `BaseTestFixture.cs`: Virtual `IFileSystem? FileSystem` property
- `AnalyzerTestFixture.cs`: `compilation.WithFileSystem(FileSystem)` in `GetDiagnostics()`
- `CodeFixTestFixture.cs`: `compilation.WithFileSystem(FileSystem)` in `GetAllReportedDiagnostics()`
- `ConfigurableAnalyzerTestFixture.cs`: Wires from config
- `ConfigurableCodeFixTestFixture.cs`: Wires from config
- `ConfigurableCodeRefactoringTestFixture.cs`: Wires from config
- `ConfigurableCompletionProviderTestFixture.cs`: Wires from config

## Known issues

- `ManifestHelper.GetManifest(compilation)` in ALCops.Common throws `FileNotFoundException` for `Microsoft.Dynamics.Nav.Analyzers.Common` in test contexts because that assembly isn't available. Analyzers using `ManifestHelper` must wrap the call in try-catch. This is NOT a RoslynTestKit issue but affects analyzers that use FileSystem-dependent features alongside ManifestHelper.

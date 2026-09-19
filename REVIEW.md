# Code review — RoslynTestKit

Correctness = changes what a consumer test reports, breaks the netstandard2.1 build, or breaks
binary compatibility of the netstandard2.1 asset.

## Check table

| Check | Why | Reasoning in |
|---|---|---|
| Option not wired in all four `Configurable*` files or missing from an `AdhocWorkspace` branch | Silent config ignored at runtime | `fixture-wiring.md` |
| Public API change without README option-table or TFM-badge update | Docs drift from reality | `CLAUDE.md` conventions |
| Roslyn type used where a Nav type exists | Wrong assembly at runtime | `CLAUDE.md` architecture |
| C# 9+ feature or SDK member unavailable on 12.0 DevTools without `#if` guard | netstandard2.1 build breaks | `workspace-compat.md` |
| New mutable static state or cached/shared workspace instance | Thread-safety regression | `workspace-compat.md` |
| Behavioural divergence between `AdhocWorkspace` branches or reflection that throws instead of falling back | TFM-specific test failures | `workspace-compat.md` |
| `Verify`/`CodeMarkup` normalization or offset change | Breaks all consumer code-fix and refactoring tests | `fixture-wiring.md` |
| DevTools version edited in fewer than all required places (csproj comment, both workflows) | CI/local mismatch | `devtools-tfm.md` |

## Do not report

- Formatting style.
- Sync-over-async patterns (`GetAwaiter().GetResult()`); this is deliberate house style.
- The known `ThrowsWhenInputDocumentContainsError` default mismatch (config `true`, fixture
  `false`) unless the change explicitly touches that property.

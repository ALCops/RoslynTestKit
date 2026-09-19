---
paths:
  - "src/RoslynTestKit/RoslynTestKit.csproj"
  - ".github/**"
---

# DevTools versions and TFM management

## Where a DevTools version appears

A version must be consistent across all these locations:

1. **Csproj comment** per conditional ItemGroup: `<!-- ms-dynamics-smb.al-X.Y.Z with AssemblyFileVersion A.B.C.D -->` (the only place the AssemblyFileVersion is recorded).
2. **Both workflows** (`pull-request.yml` and `build-and-release.yml`): `version-number` input to `setup-bc-devtools`.
3. **net10.0 `TODO` comments** in csproj and `build-and-release.yml` (remove them when the net10.0 marketplace version is finalized).

## Package pins

The netstandard2.1 ItemGroup carries six `System.*` package references at the lowest patched version with a netstandard2.0 asset. These fix transitive vulnerabilities from ApprovalTests 5.4.4 (the last version with a netstandard asset). The GHSA IDs are in the csproj comment — do not remove these pins.

Each TFM has its own `Microsoft.CodeAnalysis.Common` and `ApprovalTests` version pin:
- netstandard2.1: `Microsoft.CodeAnalysis.Common 3.1.0`, `ApprovalTests 5.4.4`
- net8.0: `Microsoft.CodeAnalysis.Common 4.8.0`, `ApprovalTests 7.0.0`
- net10.0: `Microsoft.CodeAnalysis.Common 4.14.0`, `ApprovalTests 7.0.0`

## SpecificVersion and Private

All `Microsoft.Dynamics.Nav.CodeAnalysis*.dll` references use `SpecificVersion=False` (any version of the DLL is accepted at build time) and `Private=False` (DLLs are not copied to the output — consumers provide their own).

## CI mechanics

- `setup-dotnet` installs `10.0.x` (covers all three TFMs).
- The explicit `Build` step is disabled (`if: false`); `dotnet pack` builds implicitly.
- Path filters include `src/**`, `**/*.csproj`, `.github/workflows/**` etc. but exclude `!**/*.md` — doc-only PRs do not trigger CI.
- PR workflow skips draft PRs (`github.event.pull_request.draft == false`).
- GitVersion 5.x with Mainline mode (`GitVersion.yml`). Tag format: `vX.Y.Z`. Prerelease detection: version string contains `-`.
- `dotnet-validate 0.0.1-preview.537` validates the nupkg in both workflows.

## Composite action: setup-bc-devtools

`Marketplace.ps1` queries the VS Code Marketplace API for the `ms-dynamics-smb.al` extension at the given version. `Download-BcDevToolsAsset.ps1` downloads the VSIX. `Extract-RequiredFiles.ps1` extracts `extension/bin/Analyzers/Microsoft.Dynamics.Nav.CodeAnalysis*.dll` into the target folder. These scripts can be run locally for manual DevTools setup.

## Checklist: bumping a DevTools version

1. Resolve the marketplace version with `Marketplace.ps1`.
2. Download into the TFM folder with the three scripts.
3. Read the DLL's `FileVersion` (e.g. via PowerShell `[System.Diagnostics.FileVersionInfo]::GetVersionInfo()`).
4. Update the csproj `<!-- ms-dynamics-smb.al-... -->` comment with both marketplace version and AssemblyFileVersion.
5. Update `version-number` in **both** `pull-request.yml` and `build-and-release.yml`.
6. Remove `TODO` comments if applicable.
7. Update the CLAUDE.md DevTools matrix table.
8. Build all TFMs + pack.

## Checklist: adding a new TFM

1. Add to `<TargetFrameworks>` in the csproj.
2. Add a new conditional ItemGroup with `Microsoft.CodeAnalysis.Common` and `ApprovalTests` version pins.
3. Add `HintPath` references to Nav DLLs in a new DevTools subfolder.
4. Verify `#if NET8_0_OR_GREATER` still covers the new TFM (or add a new guard).
5. Add `Setup BC DevTools` steps in **both** workflow files.
6. Update README TFM badge and dependencies snippet.
7. Update CLAUDE.md DevTools matrix.
8. Add a note to Analyzers maintainers about the new asset.

**Common mistakes:** editing only one workflow; renaming the `netstandard2.0` folder to match the `netstandard2.1` TFM (the mismatch is deliberate).

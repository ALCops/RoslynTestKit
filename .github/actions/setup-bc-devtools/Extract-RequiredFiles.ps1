<#
.SYNOPSIS
Extracts a specific subfolder from a archive file into a destination directory.

.DESCRIPTION
Given a archive file and a path inside that archive, this script:
- Deletes and recreates the destination directory.
- Normalizes the archive path to forward-slash form and enforces a trailing slash.
- Validates that the specified subfolder exists in the archive.
- Iterates matching entries and extracts files (skips directory pseudo-entries).
- Prevents path traversal by ignoring any relative paths containing "…".
- Recreates directory structure under the destination and overwrites existing files.

The match is case-insensitive on the archive entry paths. No wildcard expansion is performed on PathInArchive.

.PARAMETER DestinationPath
Absolute or relative path to the folder that will receive the extracted files. This directory is removed and recreated at start.

.PARAMETER ArchivePath
Path to the .zip file to read.

.PARAMETER PathInArchive
Subfolder inside the ZIP to extract. Use a folder path (with or without leading slash, "/" or "\" accepted). Files directly under this folder and its subfolders are extracted. Example: "tools", "content/bin", or "\payload\assets".

.EXAMPLE
PS> .\Extract-ZipSubfolder.ps1 -DestinationPath .\out -ArchivePath .\package.zip -PathInArchive tools
Extracts the "tools" folder from package.zip into .\out.

.EXAMPLE
PS> .\Extract-ZipSubfolder.ps1 -DestinationPath C:\Temp\bin -ArchivePath C:\drop\app.zip -PathInArchive content/bin
Extracts "content/bin" from app.zip into C:\Temp\bin.

.NOTES
Requires .NET’s System.IO.Compression.ZipFile. Overwrites files on extraction. Throws if the specified PathInArchive does not exist in the archive.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$DestinationPath,

    [Parameter(Mandatory = $true)]
    [string]$ArchivePath,

    [Parameter(Mandatory = $true)]
    [string]$PathInArchive
)

# Always clean destination path
Remove-Item -Path $DestinationPath -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $DestinationPath | Out-Null

# Normalize the archive subpath to ZIP's forward-slash form and ensure trailing slash
$norm = ($PathInArchive -replace '\\', '/').TrimStart('/')
if ($norm.Length -gt 0 -and -not $norm.EndsWith('/')) { $norm += '/' }

$archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)

try {
    # Validate the path exists in the archive
    $exists = $archive.Entries | Where-Object {
        ($_.FullName -replace '\\', '/').StartsWith($norm, [StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1
    if (-not $exists) {
        throw "Path '$PathInArchive' not found in archive '$ArchivePath'."
    }

    foreach ($entry in $archive.Entries) {
        $full = ($entry.FullName -replace '\\', '/')

        if (-not $full.StartsWith($norm, [StringComparison]::OrdinalIgnoreCase)) { continue }
        if ([string]::IsNullOrEmpty($entry.Name)) { continue } # directory pseudo-entries

        # Relative path after the given folder
        $rel = $full.Substring($norm.Length)

        # Guard against traversal
        if ($rel.Contains('..')) { continue }

        $destPath = Join-Path $DestinationPath ($rel -replace '/', '\')
        $destDir = Split-Path $destPath -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }

        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destPath, $true)
    }
}
finally {
    $archive.Dispose()
}
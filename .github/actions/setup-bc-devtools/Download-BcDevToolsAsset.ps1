<#
.SYNOPSIS
Downloads a BC DevTools asset to a target directory with retries and writes the full file path to stdout.

.DESCRIPTION
Validates the AssetUri, downloads the file with bounded retries and timeout, and prints the resolved path.
Use the workflow step to capture stdout and emit "path=<value>" to $GITHUB_OUTPUT.

.PARAMETER AssetUri
Absolute URI of the asset to download.

.PARAMETER MaxAttempts
Maximum download attempts. Default: 3.

.PARAMETER TimeoutSec
Per-attempt timeout in seconds. Default: 600.

.EXAMPLE
$path = ./.github/scripts/Download-BcDevToolsAsset.ps1 -AssetUri 'https://example/file.nupkg'
Write-Output "Downloaded to: $path"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AssetUri,

    [Parameter()]
    [int]$MaxAttempts = 3,

    [Parameter()]
    [int]$TimeoutSec = 600
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($AssetUri)) { 
    throw 'AssetUri is empty' 
}

try { 
    $uri = [Uri]$AssetUri
}
catch {
    throw "AssetUri is not a valid URI: $AssetUri" 
}

if (-not $uri.IsAbsoluteUri) {
    throw 'AssetUri must be absolute' 
}

$TempDirectory = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$TargetDirectory = Join-Path -Path $TempDirectory -ChildPath ("bcdevtools_{0}" -f ([Guid]::NewGuid().ToString('N')))
New-Item -ItemType Directory -Path $TargetDirectory -Force | Out-Null

$fileName = [IO.Path]::GetFileName($uri.AbsolutePath)
if ([string]::IsNullOrWhiteSpace($fileName)) { 
    throw 'Unable to resolve file name from URI' 
}
$downloadPath = Join-Path $TargetDirectory $fileName

for ($i = 1; $i -le $MaxAttempts; $i++) {
    try {
        Invoke-WebRequest -Uri $uri -OutFile $downloadPath -MaximumRedirection 5 -TimeoutSec $TimeoutSec
        if (-not (Test-Path -LiteralPath $downloadPath)) { 
            throw 'File not found after download' 
        }
        break
    }
    catch {
        if ($i -eq $MaxAttempts) { 
            throw 
        }
        Start-Sleep -Seconds ([int][Math]::Pow(2, $i))
    }
}

# Return path
Write-Output $downloadPath
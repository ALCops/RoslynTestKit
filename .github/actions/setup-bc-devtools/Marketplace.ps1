<# 
.SYNOPSIS
Return the VSIX download URL (source) for a specific AL Language extension version.

.PARAMETER Version
Exact extension version to resolve, e.g. 17.0.1750311.

.PARAMETER Publisher
Marketplace publisher short name. Default: ms-dynamics-smb

.PARAMETER Extension
Extension short name. Default: al

.PARAMETER UrlKind
Which URL to return:
- asset     -> API "assetbyname" URL from files[].source
- fallback  -> CDN URL (fallbackAssetUri + '/Microsoft.VisualStudio.Services.VSIXPackage')
Default: asset
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Publisher = 'ms-dynamics-smb',
    [string]$Extension = 'al',

    [ValidateSet('asset', 'fallback')]
    [string]$UrlKind = 'asset'
)

$ErrorActionPreference = 'Stop'

# Build the extension identifier used in filterType 7
$extensionId = "$Publisher.$Extension"

# Request payload (matches your sample; flags=147 required to get versions/files)
$payload = @{
    filters    = @(
        @{
            criteria   = @(
                # 7 = ExtensionName
                @{ filterType = 7; value = $extensionId }                  # Publisher.Extension

                # 8 = Target
                @{ filterType = 8; value = 'Microsoft.VisualStudio.Code' } # Target (Microsoft.VisualStudio.Code)
                
                # 12 = ExcludeWithFlags
                @{ filterType = 12; value = '4096' }                        # Unpublished (4096)
            )
            pageNumber = 1
            pageSize   = 50
            sortBy     = 0
            sortOrder  = 0
        }
    )
    assetTypes = @()
    flags      = 131 # IncludeVersions, IncludeFiles, IncludeAssetUri, see https://github.com/microsoft/vscode/blob/12ae331012923024bedaf873ba4259a8c64db020/src/vs/platform/extensionManagement/common/extensionGalleryService.ts#L86
}

$uri = 'https://marketplace.visualstudio.com/_apis/public/gallery/extensionquery?api-version=3.0-preview.1'

$response = Invoke-RestMethod -Method POST -Uri $uri -ContentType 'application/json' -Body ($payload | ConvertTo-Json -Depth 10)

if (-not $response.results -or -not $response.results[0].extensions) {
    throw "No extensions returned for '$extensionId'."
}

$ext = $response.results[0].extensions |
Where-Object {
    $_.extensionName -eq $Extension -and $_.publisher.publisherName -eq $Publisher
} |
Select-Object -First 1

if (-not $ext) {
    throw "Extension '$extensionId' not found."
}

$ver = $ext.versions | Where-Object { $_.version -eq $Version } | Select-Object -First 1
if (-not $ver) {
    $available = ($ext.versions | ForEach-Object version) -join ', '
    throw "Version '$Version' not found. Available: $available"
}

switch ($UrlKind) {
    'asset' {
        $file = $ver.files | Where-Object { $_.assetType -eq 'Microsoft.VisualStudio.Services.VSIXPackage' } | Select-Object -First 1
        if (-not $file) { throw "VSIXPackage asset not found for version '$Version'." }
        $url = $file.source
    }
    'fallback' {
        if (-not $ver.fallbackAssetUri) { throw "fallbackAssetUri not present for version '$Version'." }
        $url = ($ver.fallbackAssetUri.TrimEnd('/')) + '/Microsoft.VisualStudio.Services.VSIXPackage'
    }
}

# Output only the URL
$url
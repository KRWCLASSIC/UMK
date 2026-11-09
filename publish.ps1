<#
.SYNOPSIS
    Packs and publishes KRWCLASSIC.UMK to NuGet.org.

.DESCRIPTION
    1. Reads the current version from UMK.csproj.
    2. Builds and packs the project in Release mode.
    3. Pushes the .nupkg to NuGet.org using your API key.

.NOTES
    WHERE TO GET / REGENERATE YOUR NUGET API KEY:
    - Go to: https://www.nuget.org/account/apikeys?forceApiKeys=true
    - If expired, click "Create" or "Regenerate".
    - Required Scope: "Push new packages and package versions".
    - Glob pattern: "KRWCLASSIC.*" (or "*").

    IMPORTANT:
    Remember to increment <Version> in 'UMK.csproj' before publishing
    a new release! NuGet rejects re-uploading an existing version.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, HelpMessage = "Your NuGet.org API key")]
    [string]$ApiKey = $env:NUGET_API_KEY
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$csprojPath = Join-Path $repoRoot "UMK.csproj"

if (-not (Test-Path $csprojPath)) {
    Write-Error "[UMK] Could not find UMK.csproj at '$csprojPath'."
    exit 1
}

# 1. Read version from csproj
[xml]$csprojXml = Get-Content $csprojPath
$version = $csprojXml.Project.PropertyGroup.Version
if (-not $version) {
    Write-Error "[UMK] Could not detect <Version> tag in '$csprojPath'."
    exit 1
}

Write-Host "[UMK] Target release version: $version" -ForegroundColor Cyan

# 2. Ensure API key is present
if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Host ""
    Write-Host "Where to get or regenerate your key: https://www.nuget.org/account/apikeys?forceApiKeys=true" -ForegroundColor Yellow
    $ApiKey = Read-Host "Enter your NuGet API key"
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-Error "[UMK] API key cannot be empty."
    exit 1
}

# 3. Pack Release
Write-Host "`n[UMK] Packing KRWCLASSIC.UMK v$version (Release)..." -ForegroundColor Cyan
dotnet pack $csprojPath -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "[UMK] dotnet pack failed."
    exit $LASTEXITCODE
}

$nupkgPath = Join-Path $repoRoot "bin\Release\KRWCLASSIC.UMK.$version.nupkg"
if (-not (Test-Path $nupkgPath)) {
    Write-Error "[UMK] Expected package file not found at '$nupkgPath'."
    exit 1
}

# 4. Push to NuGet
Write-Host "`n[UMK] Pushing $nupkgPath to NuGet.org..." -ForegroundColor Cyan
dotnet nuget push $nupkgPath --api-key $ApiKey --source "https://api.nuget.org/v3/index.json"
if ($LASTEXITCODE -ne 0) {
    Write-Error "[UMK] dotnet nuget push failed."
    exit $LASTEXITCODE
}

Write-Host "`n[UMK] Successfully published KRWCLASSIC.UMK v$version to NuGet.org!" -ForegroundColor Green
Write-Host "URL: https://www.nuget.org/packages/KRWCLASSIC.UMK/$version" -ForegroundColor Green


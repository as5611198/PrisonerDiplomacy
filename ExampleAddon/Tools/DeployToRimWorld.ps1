[CmdletBinding()]
param(
    [string]$RimWorldDir = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$TargetDir,
    [switch]$SkipBuild,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$addonRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($TargetDir)) {
    $TargetDir = Join-Path $RimWorldDir 'Mods\PrisonerDiplomacyExampleAddon'
}

if (Get-Process -Name RimWorldWin64 -ErrorAction SilentlyContinue) {
    throw 'RimWorld is running. Close the game before deploying the Add-on DLL and content.'
}

$modsRoot = [System.IO.Path]::GetFullPath((Join-Path $RimWorldDir 'Mods'))
$targetFull = [System.IO.Path]::GetFullPath($TargetDir)
$expectedLeaf = Split-Path -Leaf $targetFull
$hasExpectedLeaf = $expectedLeaf -eq 'PrisonerDiplomacyExampleAddon'
$isInsideMods = $targetFull.StartsWith(
    $modsRoot + [System.IO.Path]::DirectorySeparatorChar,
    [System.StringComparison]::OrdinalIgnoreCase)
if (-not $hasExpectedLeaf -or -not $isInsideMods) {
    throw "Refusing deployment outside the exact RimWorld Mods target: $targetFull"
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -RimWorldDir $RimWorldDir
}

if ($Clean -and (Test-Path -LiteralPath $targetFull)) {
    Remove-Item -LiteralPath $targetFull -Recurse -Force
}
New-Item -ItemType Directory -Path $targetFull -Force | Out-Null

$directories = @('About', 'Docs', 'Source', 'Templates', 'Tools', 'Workshop')
foreach ($directory in $directories) {
    $source = Join-Path $addonRoot $directory
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $targetFull -Recurse -Force
    }
}

$versionTarget = Join-Path $targetFull '1.6'
New-Item -ItemType Directory -Path $versionTarget -Force | Out-Null
foreach ($directory in @('Defs', 'Languages', 'Textures')) {
    Copy-Item -LiteralPath (Join-Path $addonRoot "1.6\$directory") -Destination $versionTarget -Recurse -Force
}
$assemblyTarget = Join-Path $versionTarget 'Assemblies'
New-Item -ItemType Directory -Path $assemblyTarget -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $addonRoot '1.6\Assemblies\PrisonerDiplomacyExampleAddon.dll') -Destination $assemblyTarget -Force

foreach ($file in @('PrisonerDiplomacyExampleAddon.csproj', 'README.md', 'README.zh-TW.md', 'LICENSE', 'ASSET-LICENSE.md', 'NOTICE')) {
    $source = Join-Path $addonRoot $file
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $targetFull -Force
    }
}

$deployedDll = Join-Path $assemblyTarget 'PrisonerDiplomacyExampleAddon.dll'
$hash = (Get-FileHash -LiteralPath $deployedDll -Algorithm SHA256).Hash
Write-Host "[Example Add-on Deploy] Installed: $targetFull"
Write-Host "[Example Add-on Deploy] DLL SHA-256: $hash"

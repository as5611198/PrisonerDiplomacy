[CmdletBinding()]
param(
    [string]$RimWorldDir = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$OutputDirectory,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$addonRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $addonRoot 'Dist'
}
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -RimWorldDir $RimWorldDir
}
& (Join-Path $PSScriptRoot 'Validate.ps1')

$about = [xml](Get-Content -LiteralPath (Join-Path $addonRoot 'About\About.xml') -Raw -Encoding UTF8)
$version = [string]$about.ModMetaData.modVersion
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputFull = [System.IO.Path]::GetFullPath($OutputDirectory)
$zipPath = Join-Path $outputFull "PrisonerDiplomacyExampleAddon-$version.zip"

$tempBase = [System.IO.Path]::GetTempPath()
$stagingRoot = Join-Path $tempBase ("PrisonerDiplomacyExampleAddon-package-" + [guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $stagingRoot 'PrisonerDiplomacyExampleAddon'
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
try {
    foreach ($directory in @('About', 'Docs', 'Source', 'Templates', 'Tools', 'Workshop')) {
        Copy-Item -LiteralPath (Join-Path $addonRoot $directory) -Destination $packageRoot -Recurse -Force
    }

    $versionRoot = Join-Path $packageRoot '1.6'
    New-Item -ItemType Directory -Path $versionRoot -Force | Out-Null
    foreach ($directory in @('Defs', 'Languages', 'Textures')) {
        Copy-Item -LiteralPath (Join-Path $addonRoot "1.6\$directory") -Destination $versionRoot -Recurse -Force
    }
    $assemblyRoot = Join-Path $versionRoot 'Assemblies'
    New-Item -ItemType Directory -Path $assemblyRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $addonRoot '1.6\Assemblies\PrisonerDiplomacyExampleAddon.dll') -Destination $assemblyRoot -Force

    foreach ($file in @('PrisonerDiplomacyExampleAddon.csproj', 'README.md', 'README.zh-TW.md', 'LICENSE', 'ASSET-LICENSE.md', 'NOTICE')) {
        $source = Join-Path $addonRoot $file
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination $packageRoot -Force
        }
    }

    $publishedId = Join-Path $packageRoot 'About\PublishedFileId.txt'
    if (Test-Path -LiteralPath $publishedId) {
        Remove-Item -LiteralPath $publishedId -Force
    }
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
}
finally {
    $stagingFull = [System.IO.Path]::GetFullPath($stagingRoot)
    $stagingLeaf = Split-Path -Leaf $stagingFull
    $isTempChild = $stagingFull.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase)
    $isPackageStage = $stagingLeaf.StartsWith('PrisonerDiplomacyExampleAddon-package-')
    if ($isTempChild -and $isPackageStage) {
        Remove-Item -LiteralPath $stagingFull -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$zip = Get-Item -LiteralPath $zipPath
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Write-Host "[Example Add-on Package] ZIP: $($zip.FullName)"
Write-Host "[Example Add-on Package] Size: $($zip.Length) bytes"
Write-Host "[Example Add-on Package] SHA-256: $hash"

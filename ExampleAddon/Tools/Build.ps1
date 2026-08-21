[CmdletBinding()]
param(
    [string]$RimWorldDir = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$PrisonerDiplomacyRoot,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipArtwork,
    [switch]$SkipValidation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$addonRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $addonRoot
if ([string]::IsNullOrWhiteSpace($PrisonerDiplomacyRoot)) {
    $PrisonerDiplomacyRoot = $repoRoot
}

$rimWorldAssembly = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'
$coreAssembly = Join-Path $PrisonerDiplomacyRoot '1.6\Assemblies\PrisonerDiplomacy.dll'
$project = Join-Path $addonRoot 'PrisonerDiplomacyExampleAddon.csproj'

if (-not (Test-Path -LiteralPath $rimWorldAssembly)) {
    throw "RimWorld managed assembly not found: $rimWorldAssembly"
}
if (-not (Test-Path -LiteralPath $coreAssembly)) {
    throw "Prisoner Diplomacy core assembly not found: $coreAssembly"
}
if (-not $SkipArtwork) {
    & (Join-Path $PSScriptRoot 'GenerateArtwork.ps1')
}

& dotnet build $project -c $Configuration -t:Rebuild --nologo `
    "-p:RimWorldDir=$RimWorldDir" `
    "-p:PrisonerDiplomacyRoot=$PrisonerDiplomacyRoot"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipValidation) {
    & (Join-Path $PSScriptRoot 'Validate.ps1')
}

$output = Join-Path $addonRoot '1.6\Assemblies\PrisonerDiplomacyExampleAddon.dll'
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
Write-Host "[Example Add-on Build] $Configuration DLL: $output"
Write-Host "[Example Add-on Build] SHA-256: $hash"

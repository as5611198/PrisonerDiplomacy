[CmdletBinding()]
param(
    [string]$RimWorldDir = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$SavedataRoot,
    [ValidateRange(30, 300)]
    [int]$TimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$gameExe = Join-Path $RimWorldDir 'RimWorldWin64.exe'
$coreDll = Join-Path $RimWorldDir 'Mods\PrisonerDiplomacy\1.6\Assemblies\PrisonerDiplomacy.dll'
$addonDll = Join-Path $RimWorldDir 'Mods\PrisonerDiplomacyExampleAddon\1.6\Assemblies\PrisonerDiplomacyExampleAddon.dll'
foreach ($path in @($gameExe, $coreDll, $addonDll)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required smoke-test file is missing: $path"
    }
}
if (Get-Process -Name RimWorldWin64 -ErrorAction SilentlyContinue) {
    throw 'RimWorld is already running. Close it before starting an isolated load smoke test.'
}

if ([string]::IsNullOrWhiteSpace($SavedataRoot)) {
    $SavedataRoot = Join-Path ([System.IO.Path]::GetTempPath()) `
        ("CodexPDExampleAddonSmoke-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$savedataFull = [System.IO.Path]::GetFullPath($SavedataRoot)
if (Test-Path -LiteralPath $savedataFull) {
    throw "SavedataRoot already exists; choose a new isolated path: $savedataFull"
}

$configDir = Join-Path $savedataFull 'Config'
New-Item -ItemType Directory -Path $configDir -Force | Out-Null

[xml]$mods = @'
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>1.6</version>
  <activeMods>
    <li>brrainz.harmony</li>
    <li>ludeon.rimworld</li>
  </activeMods>
  <knownExpansions />
</ModsConfigData>
'@

$dlc = @(
    @{ Directory = 'Royalty'; PackageId = 'ludeon.rimworld.royalty' },
    @{ Directory = 'Ideology'; PackageId = 'ludeon.rimworld.ideology' },
    @{ Directory = 'Biotech'; PackageId = 'ludeon.rimworld.biotech' },
    @{ Directory = 'Anomaly'; PackageId = 'ludeon.rimworld.anomaly' },
    @{ Directory = 'Odyssey'; PackageId = 'ludeon.rimworld.odyssey' }
)
$activeModsNode = $mods.SelectSingleNode('/ModsConfigData/activeMods')
$knownExpansionsNode = $mods.SelectSingleNode('/ModsConfigData/knownExpansions')
foreach ($entry in $dlc) {
    if (-not (Test-Path -LiteralPath (Join-Path $RimWorldDir ("Data\" + $entry.Directory)))) {
        continue
    }
    foreach ($parent in @($activeModsNode, $knownExpansionsNode)) {
        $node = $mods.CreateElement('li')
        $node.InnerText = $entry.PackageId
        [void]$parent.AppendChild($node)
    }
}
foreach ($packageId in @('g1061.prisonerdiplomacy', 'g1061.prisonerdiplomacy.exampleaddon')) {
    $node = $mods.CreateElement('li')
    $node.InnerText = $packageId
    [void]$activeModsNode.AppendChild($node)
}
$mods.Save((Join-Path $configDir 'ModsConfig.xml'))

$logPath = Join-Path $savedataFull 'SmokeTest.log'
$arguments = @(
    "-savedatafolder=$savedataFull",
    '-logFile',
    $logPath,
    '-quicktest',
    '-pdsmoketest',
    '-popupwindow'
)
$process = $null
$terminalLine = $null
try {
    $process = Start-Process -FilePath $gameExe -ArgumentList $arguments `
        -PassThru -WindowStyle Hidden
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 500
        if (Test-Path -LiteralPath $logPath) {
            $terminalLine = Select-String -Path $logPath `
                -Pattern 'Prisoner Diplomacy SmokeTest\] (PASS|FAIL)' `
                | Select-Object -Last 1 -ExpandProperty Line
        }
        if ($terminalLine) { break }
        $process.Refresh()
    } while (-not $process.HasExited -and (Get-Date) -lt $deadline)
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $ownedProcess = Get-CimInstance Win32_Process `
            -Filter "ProcessId=$($process.Id)" -ErrorAction SilentlyContinue
        if ($ownedProcess -and $ownedProcess.CommandLine -like "*$savedataFull*") {
            Stop-Process -Id $process.Id -Force
        }
    }
}

if (-not (Test-Path -LiteralPath $logPath)) {
    throw "RimWorld did not create a smoke log. Savedata: $savedataFull"
}
$logText = Get-Content -LiteralPath $logPath -Raw
$registrationPattern = [regex]::Escape('[Prisoner Diplomacy Example Add-on] 1.0.0 initialized against API 1.2.0. extension=True persona=True ui=True.')
$addonErrorPatterns = @(
    'No textures found at path Things/Item/PDX_',
    "Could not load Texture2D at 'Things/Item/PDX_",
    'Could not translate.*PDX_',
    'Exception.*PrisonerDiplomacyExampleAddon',
    'PrisonerDiplomacyExampleAddon.*Exception'
)

if ($terminalLine -notmatch 'SmokeTest\] PASS cases=127') {
    throw "Smoke test did not pass. Terminal result: $terminalLine`nLog: $logPath"
}
if ($logText -notmatch $registrationPattern) {
    throw "Example Add-on did not report all three successful registrations. Log: $logPath"
}
foreach ($pattern in $addonErrorPatterns) {
    if ($logText -match $pattern) {
        throw "Example Add-on load error matched '$pattern'. Log: $logPath"
    }
}

Write-Host '[Example Add-on Load Smoke] PASS' -ForegroundColor Green
Write-Host "[Example Add-on Load Smoke] $terminalLine"
Write-Host '[Example Add-on Load Smoke] registration=True persona=True ui=True textures=True translations=True'
Write-Host "[Example Add-on Load Smoke] Log: $logPath"

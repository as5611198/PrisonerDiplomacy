[CmdletBinding()]
param(
    [switch]$AllowPublishedFileId,
    [switch]$SkipAssemblyCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$addonRoot = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]
$checks = 0

function Test-Requirement {
    param([bool]$Condition, [string]$Message)
    $script:checks++
    if (-not $Condition) {
        $script:failures.Add($Message)
    }
}

function Read-XmlDocument {
    param([string]$Path)
    try {
        return [xml](Get-Content -LiteralPath $Path -Raw -Encoding UTF8)
    }
    catch {
        $script:failures.Add("Invalid XML: $Path :: $($_.Exception.Message)")
        return $null
    }
}

function Get-ElementMap {
    param([xml]$Document)
    $map = @{}
    if ($null -eq $Document -or $null -eq $Document.LanguageData) {
        return $map
    }
    foreach ($node in $Document.LanguageData.ChildNodes) {
        if ($node.NodeType -eq [System.Xml.XmlNodeType]::Element) {
            $map[$node.Name] = [string]$node.InnerText
        }
    }
    return $map
}

function Get-Placeholders {
    param([string]$Text)
    if ($null -eq $Text) { return @() }
    return @([regex]::Matches($Text, '\{\d+(?::[^}]*)?\}') | ForEach-Object Value | Sort-Object -Unique)
}

$requiredFiles = @(
    'About\About.xml',
    'About\Preview.png',
    '1.6\Defs\ThingDefs\PDX_Items.xml',
    '1.6\Textures\Things\Item\PDX_DiplomaticSeal.png',
    '1.6\Textures\Things\Item\PDX_EncryptedDiplomaticLedger.png',
    'README.md',
    'README.zh-TW.md',
    'Docs\API-Cookbook.md',
    'Docs\API-Cookbook.zh-TW.md',
    'Docs\TestGuide.md',
    'LICENSE',
    'ASSET-LICENSE.md'
)
foreach ($relative in $requiredFiles) {
    Test-Requirement (Test-Path -LiteralPath (Join-Path $addonRoot $relative)) "Missing required file: $relative"
}

$publishedIdPath = Join-Path $addonRoot 'About\PublishedFileId.txt'
Test-Requirement ($AllowPublishedFileId -or -not (Test-Path -LiteralPath $publishedIdPath)) `
    'About/PublishedFileId.txt exists. Remove it for a new Workshop item or pass -AllowPublishedFileId for an existing item.'

$xmlFiles = @(Get-ChildItem -LiteralPath (Join-Path $addonRoot 'About') -Filter '*.xml' -File -ErrorAction SilentlyContinue) `
    + @(Get-ChildItem -LiteralPath (Join-Path $addonRoot '1.6') -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue)
foreach ($file in $xmlFiles) {
    [void](Read-XmlDocument $file.FullName)
}

$about = Read-XmlDocument (Join-Path $addonRoot 'About\About.xml')
if ($null -ne $about) {
    Test-Requirement ($about.ModMetaData.packageId -eq 'g1061.prisonerdiplomacy.exampleaddon') 'Unexpected packageId in About.xml.'
    Test-Requirement ($about.ModMetaData.modVersion -eq '1.0.0') 'Unexpected modVersion in About.xml.'
    Test-Requirement (@($about.ModMetaData.supportedVersions.li) -contains '1.6') 'RimWorld 1.6 is missing from supportedVersions.'
    Test-Requirement (@($about.ModMetaData.modDependencies.li.packageId) -contains 'g1061.prisonerdiplomacy') 'Core dependency is missing from About.xml.'
    Test-Requirement (@($about.ModMetaData.loadAfter.li) -contains 'g1061.prisonerdiplomacy') 'Core loadAfter entry is missing from About.xml.'
}

$languages = @('English', 'ChineseTraditional', 'ChineseSimplified', 'Japanese', 'Korean')
$englishKeyed = $null
$englishDefInjected = $null
foreach ($language in $languages) {
    $keyedPath = Join-Path $addonRoot "1.6\Languages\$language\Keyed\PrisonerDiplomacyExampleAddon.xml"
    $defPath = Join-Path $addonRoot "1.6\Languages\$language\DefInjected\ThingDef\PDX_Items.xml"
    Test-Requirement (Test-Path -LiteralPath $keyedPath) "Missing Keyed localization for $language."
    Test-Requirement (Test-Path -LiteralPath $defPath) "Missing ThingDef localization for $language."
    if (-not (Test-Path -LiteralPath $keyedPath) -or -not (Test-Path -LiteralPath $defPath)) { continue }

    $keyed = Get-ElementMap (Read-XmlDocument $keyedPath)
    $defInjected = Get-ElementMap (Read-XmlDocument $defPath)
    if ($language -eq 'English') {
        $englishKeyed = $keyed
        $englishDefInjected = $defInjected
        continue
    }

    $expectedKeys = @($englishKeyed.Keys | Sort-Object)
    $actualKeys = @($keyed.Keys | Sort-Object)
    Test-Requirement (($expectedKeys -join "`n") -eq ($actualKeys -join "`n")) "Keyed key mismatch in $language."
    foreach ($key in $expectedKeys) {
        if (-not $keyed.ContainsKey($key)) { continue }
        $expectedPlaceholders = @(Get-Placeholders $englishKeyed[$key]) -join ','
        $actualPlaceholders = @(Get-Placeholders $keyed[$key]) -join ','
        Test-Requirement ($expectedPlaceholders -eq $actualPlaceholders) "Placeholder mismatch for $language key $key."
    }

    $expectedDefKeys = @($englishDefInjected.Keys | Sort-Object)
    $actualDefKeys = @($defInjected.Keys | Sort-Object)
    Test-Requirement (($expectedDefKeys -join "`n") -eq ($actualDefKeys -join "`n")) "DefInjected key mismatch in $language."
}

$workshopDescriptions = @(
    'Description.en.txt',
    'Description.zh-TW.txt',
    'Description.zh-CN.txt',
    'Description.ja.txt',
    'Description.ko.txt'
)
foreach ($file in $workshopDescriptions) {
    $path = Join-Path $addonRoot "Workshop\$file"
    Test-Requirement (Test-Path -LiteralPath $path) "Missing Workshop description: $file"
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $bytes = [System.Text.Encoding]::UTF8.GetByteCount($content)
    Test-Requirement ($bytes -le 7500) "$file exceeds the 7500-byte Workshop copy budget."
    Test-Requirement ($content.Contains('[h1]') -and $content.Contains('[h2]') -and $content.Contains('[list]')) "$file is missing required Steam BBCode structure."
    Test-Requirement ($content.Contains('3787243156')) "$file is missing the core Workshop dependency link."
    Test-Requirement ($content.Contains('github.com/as5611198/PrisonerDiplomacy/tree/main/ExampleAddon')) "$file is missing the Example Add-on source link."
    Test-Requirement ($content.Contains('GPT-5.6 SOL')) "$file is missing the project authorship statement."
    Test-Requirement ([regex]::Matches($content, '\]\(https?://').Count -eq 0) "$file contains Markdown links instead of Steam BBCode links."
}

$defs = Read-XmlDocument (Join-Path $addonRoot '1.6\Defs\ThingDefs\PDX_Items.xml')
if ($null -ne $defs) {
    foreach ($thingDef in @($defs.Defs.ThingDef)) {
        $defName = [string]$thingDef.defName
        $texPath = [string]$thingDef.graphicData.texPath
        $graphicClass = [string]$thingDef.graphicData.graphicClass
        $stackLimit = [int]$thingDef.stackLimit
        $marketValue = [decimal]$thingDef.statBases.MarketValue
        Test-Requirement (-not [string]::IsNullOrWhiteSpace($defName)) 'ThingDef has no defName.'
        Test-Requirement ($stackLimit -gt 0) "$defName must have a positive stackLimit."
        Test-Requirement ($marketValue -gt 0) "$defName must have a positive MarketValue."
        Test-Requirement ($graphicClass -eq 'Graphic_Single') "$defName uses a single generated texture and must use Graphic_Single."
        $texturePath = Join-Path $addonRoot ("1.6\Textures\" + $texPath.Replace('/', '\\') + '.png')
        Test-Requirement (Test-Path -LiteralPath $texturePath) "$defName references missing texture $texPath."
    }
}

Add-Type -AssemblyName System.Drawing
$previewPath = Join-Path $addonRoot 'About\Preview.png'
if (Test-Path -LiteralPath $previewPath) {
    $preview = [System.Drawing.Image]::FromFile($previewPath)
    try {
        Test-Requirement ($preview.Width -eq 860 -and $preview.Height -eq 480) 'Preview.png must be 860x480.'
    }
    finally {
        $preview.Dispose()
    }
    Test-Requirement ((Get-Item -LiteralPath $previewPath).Length -lt 1MB) 'Preview.png must be smaller than 1 MB.'
}
$workshopCoverPath = Join-Path $addonRoot 'Workshop\Artwork\PrisonerDiplomacyExampleAddon-cover-en.png'
Test-Requirement (Test-Path -LiteralPath $workshopCoverPath) 'Workshop cover is missing.'
if (Test-Path -LiteralPath $workshopCoverPath) {
    $workshopCover = [System.Drawing.Image]::FromFile($workshopCoverPath)
    try {
        Test-Requirement ($workshopCover.Width -eq 860 -and $workshopCover.Height -eq 480) 'Workshop cover must be 860x480.'
    }
    finally {
        $workshopCover.Dispose()
    }
    Test-Requirement ((Get-Item -LiteralPath $workshopCoverPath).Length -lt 1MB) 'Workshop cover must be smaller than 1 MB.'
}

$sourceText = (Get-ChildItem -LiteralPath (Join-Path $addonRoot 'Source') -Filter '*.cs' -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
}) -join "`n"
$forbiddenPatterns = @(
    'PrisonerDiplomacyGameComponent',
    '\[HarmonyPatch',
    'AccessTools\.',
    'ThingMaker\.',
    'GenSpawn\.',
    'GetComponent<PrisonerDiplomacy'
)
foreach ($pattern in $forbiddenPatterns) {
    Test-Requirement (-not [regex]::IsMatch($sourceText, $pattern)) "Forbidden internal/mutation pattern in Example Add-on source: $pattern"
}

$assemblyDir = Join-Path $addonRoot '1.6\Assemblies'
Test-Requirement (-not (Test-Path -LiteralPath (Join-Path $assemblyDir 'PrisonerDiplomacy.dll'))) 'Do not package PrisonerDiplomacy.dll inside the Add-on.'
if (-not $SkipAssemblyCheck) {
    $addonAssembly = Join-Path $assemblyDir 'PrisonerDiplomacyExampleAddon.dll'
    Test-Requirement (Test-Path -LiteralPath $addonAssembly) 'Built Example Add-on DLL is missing.'
    if (Test-Path -LiteralPath $addonAssembly) {
        $stream = [System.IO.File]::OpenRead($addonAssembly)
        try {
            $peReader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
            try {
                $metadataReader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($peReader)
                $referenceNames = @($metadataReader.AssemblyReferences | ForEach-Object {
                    $reference = $metadataReader.GetAssemblyReference($_)
                    $metadataReader.GetString($reference.Name)
                })
            }
            finally {
                $peReader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
        Test-Requirement ($referenceNames -contains 'PrisonerDiplomacy') 'Built DLL does not reference the public PrisonerDiplomacy assembly.'
    }
}

if ($failures.Count -gt 0) {
    Write-Host "[Example Add-on Validate] FAIL ($($failures.Count) failure(s), $checks checks)" -ForegroundColor Red
    foreach ($failure in $failures) { Write-Host " - $failure" -ForegroundColor Red }
    throw 'Prisoner Diplomacy Example Add-on validation failed.'
}

Write-Host "[Example Add-on Validate] PASS checks=$checks languages=$($languages.Count) xml=$($xmlFiles.Count)" -ForegroundColor Green

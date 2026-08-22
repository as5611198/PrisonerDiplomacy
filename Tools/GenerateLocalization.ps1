param(
    [string]$SourcePath = "1.6/Languages/English/Keyed/PrisonerDiplomacy.xml"
)

$ErrorActionPreference = "Stop"
$targets = @(
    @{ Name = "Japanese"; Code = "ja" },
    @{ Name = "Korean"; Code = "ko" },
    @{ Name = "ChineseSimplified"; Code = "zh-CN" }
)
$languageRoot = Split-Path (Split-Path (Split-Path $SourcePath -Parent) -Parent) -Parent

function Protect-Tokens([string]$value) {
    $placeholderIndex = 0
    $value = [regex]::Replace($value, '\{(\d+)\}', {
        param($match)
        $token = "[PDPH${placeholderIndex}_$($match.Groups[1].Value)]"
        $placeholderIndex++
        $token
    })
    $newlineIndex = 0
    return [regex]::Replace($value, '\\n', {
        param($match)
        $token = "[PDNL${newlineIndex}]"
        $newlineIndex++
        $token
    })
}

function Restore-Tokens([string]$value) {
    $value = [regex]::Replace($value, '\[PDPH\d+_(\d+)\]', '{$1}')
    return [regex]::Replace($value, '\[PDNL\d+\]', '\n')
}

function Translate-Text([string]$value, [string]$language) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $value }
    $protected = Protect-Tokens $value
    $query = [uri]::EscapeDataString($protected)
    $uri = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=$language&dt=t&q=$query"
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $uri -TimeoutSec 30
            $parts = @($response[0] | ForEach-Object { $_[0] })
            $translated = ($parts -join "")
            if (-not [string]::IsNullOrWhiteSpace($translated)) {
                return Restore-Tokens $translated
            }
        } catch {
            if ($attempt -eq 4) { throw }
            Start-Sleep -Milliseconds (500 * $attempt)
        }
    }
    throw "Translation failed for: $value"
}

[xml]$source = Get-Content -Raw -Encoding UTF8 $SourcePath
$sourceNodes = @($source.LanguageData.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
if ($sourceNodes.Count -ne 578) { throw "Expected 578 source keys, found $($sourceNodes.Count)." }

foreach ($target in $targets) {
    $targetDir = Join-Path $languageRoot "$($target.Name)\Keyed"
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    $output = New-Object System.Xml.XmlDocument
    $declaration = $output.CreateXmlDeclaration("1.0", "utf-8", $null)
    $output.AppendChild($declaration) | Out-Null
    $root = $output.CreateElement("LanguageData")
    $output.AppendChild($root) | Out-Null

    $cache = @{}
    $index = 0
    foreach ($sourceNode in $sourceNodes) {
        $index++
        $element = $output.CreateElement($sourceNode.Name)
        $sourceText = $sourceNode.InnerText
        if ($cache.ContainsKey($sourceText)) {
            $translatedText = $cache[$sourceText]
        } else {
            $translatedText = Translate-Text $sourceText $target.Code
            $sourcePlaceholders = @([regex]::Matches($sourceText, '\{\d+\}')).Count
            $targetPlaceholders = @([regex]::Matches($translatedText, '\{\d+\}')).Count
            $sourceNewlines = ([regex]::Matches($sourceText, '\\n')).Count
            $targetNewlines = ([regex]::Matches($translatedText, '\\n')).Count
            if ($sourcePlaceholders -ne $targetPlaceholders -or $sourceNewlines -ne $targetNewlines) {
                throw "Translation changed formatting tokens for $($sourceNode.Name): placeholders $sourcePlaceholders/$targetPlaceholders, newlines $sourceNewlines/$targetNewlines"
            }
            $cache[$sourceText] = $translatedText
            Start-Sleep -Milliseconds 80
        }
        $element.InnerText = $translatedText
        $root.AppendChild($element) | Out-Null
        if (($index % 50) -eq 0) { Write-Host "$($target.Name): $index/$($sourceNodes.Count)" }
    }

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $true
    $settings.NewLineChars = "`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::None
    $outputPath = Join-Path $targetDir "PrisonerDiplomacy.xml"
    $writer = [System.Xml.XmlWriter]::Create($outputPath, $settings)
    $output.Save($writer)
    $writer.Dispose()
    Write-Host "Wrote $outputPath"
}

$ErrorActionPreference = "Stop"
$sourcePath = "1.6/Languages/English/Keyed/PrisonerDiplomacy.xml"
$languages = @("ChineseTraditional", "Japanese", "Korean", "ChineseSimplified")

function Get-Elements([xml]$document) {
    @($document.LanguageData.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
}

function Get-Placeholders([string]$value) {
    @([regex]::Matches($value, '\{\d+\}') | ForEach-Object { $_.Value } | Sort-Object) -join ","
}

function Get-NewlineCount([string]$value) {
    ([regex]::Matches($value, '\\n')).Count
}

$source = [xml](Get-Content -Raw -Encoding UTF8 $sourcePath)
$sourceElements = Get-Elements $source
$sourceKeys = @($sourceElements | ForEach-Object Name)
if ($sourceKeys.Count -ne 578) { throw "English source has $($sourceKeys.Count) keys; expected 578." }

$failed = $false
foreach ($language in $languages) {
    $path = "1.6/Languages/$language/Keyed/PrisonerDiplomacy.xml"
    if (-not (Test-Path -LiteralPath $path)) { Write-Error "Missing $path"; $failed = $true; continue }
    try { $document = [xml](Get-Content -Raw -Encoding UTF8 $path) } catch { Write-Error "Invalid XML: $path - $($_.Exception.Message)"; $failed = $true; continue }
    $elements = Get-Elements $document
    $keys = @($elements | ForEach-Object Name)
    $duplicateKeys = @($keys | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
    $missing = @($sourceKeys | Where-Object { $_ -notin $keys })
    $extra = @($keys | Where-Object { $_ -notin $sourceKeys })
    $badValues = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $sourceElements.Count; $i++) {
        if ($i -ge $elements.Count -or $sourceElements[$i].Name -ne $elements[$i].Name) {
            $badValues.Add("key order mismatch at index $i")
            continue
        }
        $sourceValue = $sourceElements[$i].InnerText
        $targetValue = $elements[$i].InnerText
        if ((Get-Placeholders $sourceValue) -ne (Get-Placeholders $targetValue)) { $badValues.Add("$($sourceElements[$i].Name): placeholders") }
        if ((Get-NewlineCount $sourceValue) -ne (Get-NewlineCount $targetValue)) { $badValues.Add("$($sourceElements[$i].Name): newlines") }
        if ($targetValue -match 'PDPH|PDNL|ZZPD') { $badValues.Add("$($sourceElements[$i].Name): residual token") }
        if ($targetValue -match '(?<![A-Za-z])E(?=\s*[\p{IsCJKUnifiedIdeographs}\p{IsHangulSyllables}])') { $badValues.Add("$($sourceElements[$i].Name): translation artifact") }
    }
    $ok = $elements.Count -eq $sourceElements.Count -and $duplicateKeys.Count -eq 0 -and $missing.Count -eq 0 -and $extra.Count -eq 0 -and $badValues.Count -eq 0
    if (-not $ok) { $failed = $true }
    Write-Host ("{0}: keys={1}, duplicates={2}, missing={3}, extra={4}, valueIssues={5}, result={6}" -f $language, $elements.Count, $duplicateKeys.Count, $missing.Count, $extra.Count, $badValues.Count, $(if ($ok) { "PASS" } else { "FAIL" }))
    if ($badValues.Count -gt 0) { $badValues | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" } }
}
if ($failed) { exit 1 }

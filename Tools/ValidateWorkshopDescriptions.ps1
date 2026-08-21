[CmdletBinding()]
param(
    [int]$MaxUtf8Bytes = 7500
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$files = @(
    'Workshop/SteamDescription.en.txt',
    'Workshop/SteamDescription.zh-TW.txt',
    'Workshop/SteamDescription.zh-CN.txt',
    'Workshop/SteamDescription.ja.txt',
    'Workshop/SteamDescription.ko.txt'
)
$required = @(
    '[h1]',
    '[h2]',
    '[list]',
    '[url=https://github.com/as5611198/PrisonerDiplomacy]',
    '1.2.0',
    'ai.aiyuhub.com',
    '211784688',
    'GPT-5.6 SOL'
)

$failed = $false
foreach ($relative in $files) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host "FAIL $relative missing"
        $failed = $true
        continue
    }

    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $bytes = [Text.Encoding]::UTF8.GetByteCount($content)
    $missing = @($required | Where-Object { -not $content.Contains($_) })
    $markdownLinks = [regex]::Matches($content, '\]\(https?://').Count

    if ($bytes -gt $MaxUtf8Bytes -or $missing.Count -gt 0 -or $markdownLinks -gt 0) {
        Write-Host ("FAIL {0} bytes={1}/{2} missing={3} markdownLinks={4}" -f `
            $relative, $bytes, $MaxUtf8Bytes, ($missing -join ','), $markdownLinks)
        $failed = $true
    }
    else {
        Write-Host ("PASS {0} bytes={1}/{2}" -f $relative, $bytes, $MaxUtf8Bytes)
    }
}

if ($failed) {
    exit 1
}

Write-Host 'Workshop description validation passed.'

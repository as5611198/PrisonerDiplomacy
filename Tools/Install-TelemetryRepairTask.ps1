[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')]
    [string]$Environment = 'production',
    [string]$RepositoryRoot,
    [string]$TaskName = 'PrisonerDiplomacy-TelemetryRepair'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $RepositoryRoot) {
    $RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}
else {
    $RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
}
$runner = Join-Path $RepositoryRoot 'Tools\Invoke-TelemetryRepair.ps1'
$tokenFileName = if ($Environment -eq 'staging') { '.dev.vars' } else { '.production-admin-token' }
$tokenFile = Join-Path $RepositoryRoot "Backend\PrisonerDiplomacyTelemetry\$tokenFileName"
if (-not (Test-Path -LiteralPath $runner -PathType Leaf)) {
    throw "repair runner not found: $runner"
}
if (-not (Test-Path -LiteralPath $tokenFile -PathType Leaf)) {
    throw "admin token file not found: $tokenFile"
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI is required to publish review pull requests'
}

$pwsh = (Get-Command pwsh -ErrorAction Stop).Source
$arguments = "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$runner`" -Environment $Environment -PublishPullRequest"
$action = New-ScheduledTaskAction -Execute $pwsh -Argument $arguments -WorkingDirectory $RepositoryRoot
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(2) `
    -RepetitionInterval (New-TimeSpan -Minutes 30) `
    -RepetitionDuration (New-TimeSpan -Days 3650)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 30)
$principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().Name) `
    -LogonType Interactive -RunLevel Limited

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal -Description 'Validates Prisoner Diplomacy telemetry repair candidates in an isolated Git worktree and opens review PRs.' -Force | Out-Null
Get-ScheduledTask -TaskName $TaskName | Select-Object TaskName, State

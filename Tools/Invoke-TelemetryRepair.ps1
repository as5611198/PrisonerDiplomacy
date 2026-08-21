[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')]
    [string]$Environment = 'production',
    [string]$ApiBaseUrl,
    [string]$AdminToken,
    [string]$AdminTokenFile,
    [string]$RepositoryRoot,
    [ValidateRange(1, 20)]
    [int]$MaximumCandidates = 3,
    [string]$RimWorldRoot = 'E:\SteamLibrary\steamapps\common\RimWorld',
    [string]$InstalledModRoot = 'E:\SteamLibrary\steamapps\common\RimWorld\Mods\PrisonerDiplomacy',
    [string]$ReportRoot,
    [string]$CandidateFile,
    [switch]$PublishPullRequest,
    [switch]$ValidationOnly,
    [switch]$SkipSmokeTest,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Value)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
    $digest = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($digest).ToLowerInvariant()
}

function Test-AllowedPatchPath {
    param([Parameter(Mandatory)][string]$Path)
    $normalized = $Path.Replace('\', '/')
    if ($normalized -match '(^|/)\.\.(/|$)' -or [IO.Path]::IsPathRooted($normalized)) {
        return $false
    }
    $normalized = $normalized -replace '^\./', ''
    return $normalized -match '^Source/PrisonerDiplomacy/.+\.cs$' `
        -or $normalized -match '^1\.6/Defs/.+\.xml$' `
        -or $normalized -match '^1\.6/Languages/[^/]+/Keyed/PrisonerDiplomacy\.xml$'
}

function Assert-NoDangerousAdditions {
    param([Parameter(Mandatory)][string]$Patch)
    $addedLines = $Patch -split "`r?`n" |
        Where-Object { $_.StartsWith('+') -and -not $_.StartsWith('+++') } |
        ForEach-Object { $_.Substring(1) }
    $addedText = $addedLines -join "`n"
    $blocked = '(?i)(System\.Diagnostics|\bProcess\s*\.|DllImport|Assembly\s*\.\s*Load|System\.Reflection|Microsoft\.Win32|\bRegistry\s*\.|System\.IO\.(File|Directory)|\b(File|Directory)\s*\.|HttpClient|WebRequest|WebClient|\b(Socket|TcpClient|UdpClient)\b|Environment\s*\.|AppDomain|\bunsafe\b|powershell|cmd\.exe)'
    if ($addedText -match $blocked) {
        throw "candidate contains a blocked executable or host-access pattern: $($Matches[1])"
    }
}

function Assert-UnifiedPatch {
    param([Parameter(Mandatory)][string]$Patch)
    if ([string]::IsNullOrWhiteSpace($Patch) -or $Patch.Length -gt 200000) {
        throw 'candidate patch is empty or exceeds 200000 characters'
    }
    if ($Patch -notmatch '(?m)^diff --git a/.+ b/.+$' -or $Patch -notmatch '(?m)^@@ ') {
        throw 'candidate patch is not a git unified diff'
    }
    Assert-NoDangerousAdditions -Patch $Patch
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [string]$LogPath
    )
    Push-Location -LiteralPath $WorkingDirectory
    try {
        $lines = @(& $FilePath @Arguments 2>&1 | ForEach-Object { $_.ToString() })
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($LogPath) {
        [IO.File]::WriteAllLines($LogPath, $lines, [Text.UTF8Encoding]::new($false))
    }
    if ($exitCode -ne 0) {
        $tail = ($lines | Select-Object -Last 12) -join "`n"
        throw "$FilePath exited with code $exitCode`n$tail"
    }
    return $lines
}

function Get-AdminHeaders {
    return @{ Authorization = "Bearer $script:ResolvedAdminToken" }
}

function Read-AdminTokenFile {
    param([Parameter(Mandatory)][string]$Path)
    if ([IO.Path]::GetFileName($Path) -eq '.dev.vars') {
        $line = Get-Content -LiteralPath $Path |
            Where-Object { $_ -match '^\s*ADMIN_TOKEN\s*=' } |
            Select-Object -First 1
        if (-not $line) {
            return ''
        }
        return (($line -split '=', 2)[1]).Trim().Trim('"').Trim("'")
    }
    return (Get-Content -Raw -LiteralPath $Path).Trim()
}

function Invoke-AdminRequest {
    param(
        [Parameter(Mandatory)][ValidateSet('GET', 'PATCH')][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        [object]$Body
    )
    $parameters = @{
        Uri = "$script:ResolvedApiBaseUrl$Path"
        Method = $Method
        Headers = Get-AdminHeaders
        ContentType = 'application/json'
    }
    if ($null -ne $Body) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 8 -Compress
    }
    return Invoke-RestMethod @parameters
}

function Set-IssueStatus {
    param(
        [Parameter(Mandatory)][string]$Hash,
        [Parameter(Mandatory)][ValidateSet('analyzing', 'needs_repro')][string]$Status
    )
    $null = Invoke-AdminRequest -Method PATCH -Path "/api/admin/issues/$Hash" -Body @{ status = $Status }
}

function Get-ChangedPaths {
    param([Parameter(Mandatory)][string]$Worktree)
    $tracked = @(& git -C $Worktree diff --name-only --diff-filter=ACMRTUXB --)
    if ($LASTEXITCODE -ne 0) {
        throw 'git diff failed while validating the candidate'
    }
    $untracked = @(& git -C $Worktree ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw 'git ls-files failed while validating the candidate'
    }
    return @($tracked + $untracked | Where-Object { $_ } | Sort-Object -Unique)
}

function Assert-ChangedPaths {
    param([Parameter(Mandatory)][string[]]$Paths)
    if ($Paths.Count -eq 0) {
        throw 'candidate patch made no repository changes'
    }
    $disallowed = @($Paths | Where-Object { -not (Test-AllowedPatchPath -Path $_) })
    if ($disallowed.Count -gt 0) {
        throw "candidate changed a disallowed path: $($disallowed -join ', ')"
    }
}

function Invoke-SmokeTest {
    param(
        [Parameter(Mandatory)][string]$Worktree,
        [Parameter(Mandatory)][string[]]$CandidatePaths,
        [Parameter(Mandatory)][string]$SmokeLogPath
    )
    $rimWorldExe = Join-Path $RimWorldRoot 'RimWorldWin64.exe'
    $modsRoot = [IO.Path]::GetFullPath((Join-Path $RimWorldRoot 'Mods'))
    $installedRoot = [IO.Path]::GetFullPath($InstalledModRoot)
    if (-not (Test-Path -LiteralPath $rimWorldExe -PathType Leaf)) {
        throw "RimWorld executable not found: $rimWorldExe"
    }
    if (-not $installedRoot.StartsWith($modsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'installed mod path is outside the expected RimWorld Mods directory'
    }
    if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) {
        throw 'RimWorld is already running; refusing to replace the test assembly'
    }

    $candidateAssembly = Join-Path $Worktree '1.6\Assemblies\PrisonerDiplomacy.dll'
    if (-not (Test-Path -LiteralPath $candidateAssembly -PathType Leaf)) {
        throw 'candidate assembly is missing after build'
    }

    $backupRoot = Join-Path ([IO.Path]::GetTempPath()) ("pd-repair-backup-" + [Guid]::NewGuid().ToString('N'))
    $savedataRoot = 'C:\CodexPDTest'
    $configRoot = Join-Path $savedataRoot 'Config'
    New-Item -ItemType Directory -Path $backupRoot, $configRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'TestData\Config\ModsConfig.xml') -Destination (Join-Path $configRoot 'ModsConfig.xml') -Force

    $relativeFiles = @('1.6/Assemblies/PrisonerDiplomacy.dll')
    $relativeFiles += @($CandidatePaths | Where-Object { $_ -like '1.6/Defs/*' -or $_ -like '1.6/Languages/*' })
    $relativeFiles = @($relativeFiles | Sort-Object -Unique)
    $restoreRecords = @()
    $process = $null
    try {
        foreach ($relative in $relativeFiles) {
            $platformRelative = $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
            $source = Join-Path $Worktree $platformRelative
            $target = [IO.Path]::GetFullPath((Join-Path $installedRoot $platformRelative))
            if (-not $target.StartsWith($installedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw "candidate target escaped the installed mod root: $relative"
            }
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                throw "candidate output is missing: $relative"
            }
            $backup = Join-Path $backupRoot ([Convert]::ToHexString([Text.Encoding]::UTF8.GetBytes($relative)))
            $existed = Test-Path -LiteralPath $target -PathType Leaf
            if ($existed) {
                Copy-Item -LiteralPath $target -Destination $backup -Force
            }
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
            $restoreRecords += [pscustomobject]@{ Target = $target; Backup = $backup; Existed = $existed }
        }

        if (Test-Path -LiteralPath $SmokeLogPath) {
            Remove-Item -LiteralPath $SmokeLogPath -Force
        }
        $arguments = @(
            "-savedatafolder=$savedataRoot",
            '-logFile', ('"' + $SmokeLogPath + '"'),
            '-quicktest',
            '-pdsmoketest',
            '-popupwindow'
        )
        $process = Start-Process -FilePath $rimWorldExe -ArgumentList $arguments -WindowStyle Normal -PassThru
        $deadline = (Get-Date).AddMinutes(5)
        $passed = $false
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 1
            if (Test-Path -LiteralPath $SmokeLogPath) {
                $tail = Get-Content -LiteralPath $SmokeLogPath -Tail 120 -ErrorAction SilentlyContinue
                if ($tail -match 'Prisoner Diplomacy SmokeTest\] PASS cases=127') {
                    $passed = $true
                    break
                }
                if ($tail -match 'Prisoner Diplomacy SmokeTest\] FAIL') {
                    throw 'RimWorld smoke test reported FAIL'
                }
            }
            if ($process.HasExited) {
                break
            }
            $process.Refresh()
        }
        if (-not $passed) {
            throw 'RimWorld smoke test did not reach PASS cases=127 within five minutes'
        }
    }
    finally {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $process) {
            $process.WaitForExit(10000) | Out-Null
        }
        $restoreErrors = @()
        for ($restoreIndex = $restoreRecords.Count - 1; $restoreIndex -ge 0; $restoreIndex--) {
            $record = $restoreRecords[$restoreIndex]
            for ($attempt = 1; $attempt -le 20; $attempt++) {
                try {
                    if ($record.Existed) {
                        Copy-Item -LiteralPath $record.Backup -Destination $record.Target -Force
                    }
                    elseif (Test-Path -LiteralPath $record.Target -PathType Leaf) {
                        Remove-Item -LiteralPath $record.Target -Force
                    }
                    break
                }
                catch {
                    if ($attempt -lt 20) {
                        Start-Sleep -Milliseconds 250
                    }
                    else {
                        $restoreErrors += "$($record.Target): $($_.Exception.Message)"
                    }
                }
            }
        }
        if ($restoreErrors.Count -eq 0 -and (Test-Path -LiteralPath $backupRoot)) {
            Get-ChildItem -LiteralPath $backupRoot -File | Remove-Item -Force
            Remove-Item -LiteralPath $backupRoot -Force
        }
        if ($restoreErrors.Count -gt 0) {
            throw "candidate test files could not be restored; backups retained at $backupRoot`n$($restoreErrors -join "`n")"
        }
    }
}

function Write-RepairReport {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object]$Issue,
        [Parameter(Mandatory)][object]$Candidate,
        [Parameter(Mandatory)][string]$CandidateSha,
        [Parameter(Mandatory)][string]$Outcome,
        [string[]]$ChangedPaths = @(),
        [string]$Branch = '',
        [string]$PullRequestUrl = '',
        [string]$Failure = ''
    )
    $rootCause = ([string]$Candidate.root_cause).Replace('```', "''' ").Substring(0, [Math]::Min(4000, ([string]$Candidate.root_cause).Length))
    $risks = @($Candidate.risks | ForEach-Object { '- ' + ([string]$_).Replace("`r", ' ').Replace("`n", ' ') })
    $body = @(
        '# Telemetry Repair Verification',
        '',
        "- Error hash: ``$($Issue.hash)``",
        "- Candidate SHA-256: ``$CandidateSha``",
        "- Outcome: **$Outcome**",
        "- Occurrences: $($Issue.occurrence_count)",
        "- Exception: ``$($Issue.exception_type)``",
        "- Operation: ``$($Issue.operation)``",
        "- Branch: ``$Branch``",
        "- Pull request: $PullRequestUrl",
        '',
        '## Changed Files',
        ''
    )
    $body += if ($ChangedPaths.Count -gt 0) { @($ChangedPaths | ForEach-Object { "- ``$_``" }) } else { '- None' }
    $body += @('', '## Candidate Root Cause', '', '```text', $rootCause, '```', '', '## Candidate Risks', '')
    $body += if ($risks.Count -gt 0) { $risks } else { '- None supplied' }
    if ($Failure) {
        $safeFailure = $Failure.Replace('```', "''' ")
        $body += @('', '## Failure', '', '```text', $safeFailure, '```')
    }
    $body += @('', 'This report is evidence for human review. It does not mark the telemetry issue as resolved.')
    [IO.File]::WriteAllLines($Path, $body, [Text.UTF8Encoding]::new($false))
}

function Write-PullRequestBody {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Hash,
        [Parameter(Mandatory)][string]$CandidateSha,
        [Parameter(Mandatory)][string[]]$ChangedPaths
    )
    $body = @(
        '# Telemetry Repair Candidate',
        '',
        "- Error group: ``$($Hash.Substring(0, 12))``",
        "- Candidate SHA-256: ``$CandidateSha``",
        '- Validation: Release build, localization validation, and RimWorld Smoke Test passed',
        '',
        '## Changed Files',
        ''
    )
    $body += if ($ChangedPaths.Count -gt 0) { @($ChangedPaths | ForEach-Object { "- ``$_``" }) } else { '- None' }
    $body += @(
        '',
        'Private telemetry samples and model-generated diagnosis are intentionally omitted from this public pull request.',
        'A human must review the code diff before merge; this PR does not mark the issue resolved.'
    )
    [IO.File]::WriteAllLines($Path, $body, [Text.UTF8Encoding]::new($false))
}

function Assert-SourceRefCompatible {
    param([Parameter(Mandatory)][string]$SourceRef)
    if ($SourceRef -notmatch '^[0-9a-f]{40}$') {
        throw 'candidate source_ref is missing or is not a full Git commit SHA'
    }
    & git -C $RepositoryRoot cat-file -e "${SourceRef}^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "candidate source_ref is not available locally: $SourceRef"
    }
    & git -C $RepositoryRoot diff --quiet $SourceRef HEAD -- Source/PrisonerDiplomacy 1.6/Defs 1.6/Languages
    if ($LASTEXITCODE -eq 1) {
        throw "mod source changed after candidate source_ref; regenerate the repair candidate against current HEAD"
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'git failed while comparing candidate source_ref to current HEAD'
    }
}

function Invoke-Candidate {
    param([Parameter(Mandatory)][object]$Issue)
    $hash = [string]$Issue.hash
    if ($hash -notmatch '^[0-9a-f]{64}$') {
        throw "invalid issue hash: $hash"
    }
    $candidateJson = if ($Issue.repair_candidate_json -is [string]) {
        [string]$Issue.repair_candidate_json
    }
    else {
        $Issue.repair_candidate_json | ConvertTo-Json -Depth 16 -Compress
    }
    $candidate = $candidateJson | ConvertFrom-Json -Depth 16
    $candidateSha = Get-Sha256Hex -Value $candidateJson
    $reportDirectory = Join-Path $ReportRoot (Join-Path $hash $candidateSha.Substring(0, 16))
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $reportPath = Join-Path $reportDirectory 'report.md'
    $buildLogPath = Join-Path $reportDirectory 'build.log'
    $localizationLogPath = Join-Path $reportDirectory 'localization.log'
    $smokeLogPath = Join-Path $reportDirectory 'smoke.log'
    $contextPath = Join-Path $reportDirectory 'telemetry-context.json'
    $branch = "codex/telemetry-$($hash.Substring(0, 12))-$($candidateSha.Substring(0, 8))"
    $worktree = Join-Path ([IO.Path]::GetTempPath()) ("pd-repair-worktree-" + [Guid]::NewGuid().ToString('N'))
    $patchPath = Join-Path ([IO.Path]::GetTempPath()) ("pd-repair-patch-" + [Guid]::NewGuid().ToString('N') + '.diff')
    $prBodyPath = Join-Path ([IO.Path]::GetTempPath()) ("pd-repair-pr-" + [Guid]::NewGuid().ToString('N') + '.md')
    $changedPaths = @()
    $pullRequestUrl = ''
    $worktreeAdded = $false
    try {
        if (-not $script:LocalCandidateMode) {
            $issueDetail = Invoke-AdminRequest -Method GET -Path "/api/admin/issues/$hash"
            $eventPayloads = @()
            foreach ($event in @($issueDetail.events)) {
                $eventPayloads += Invoke-AdminRequest -Method GET -Path "/api/admin/events/$($event.event_id)"
            }
            $context = [ordered]@{
                captured_at = (Get-Date).ToUniversalTime().ToString('o')
                issue = $issueDetail.issue
                events = $eventPayloads
            }
            [IO.File]::WriteAllText(
                $contextPath,
                ($context | ConvertTo-Json -Depth 20),
                [Text.UTF8Encoding]::new($false)
            )
        }
        $patchText = [string]$candidate.patch
        if (-not $patchText.EndsWith("`n")) {
            $patchText += "`n"
        }
        Assert-UnifiedPatch -Patch $patchText
        if (-not $script:LocalCandidateMode) {
            $sourceRefProperty = $candidate.PSObject.Properties['source_ref']
            $sourceRef = if ($null -ne $sourceRefProperty) { [string]$sourceRefProperty.Value } else { '' }
            Assert-SourceRefCompatible -SourceRef $sourceRef
        }
        [IO.File]::WriteAllText($patchPath, $patchText, [Text.UTF8Encoding]::new($false))
        Invoke-NativeCommand -FilePath git -Arguments @('-C', $RepositoryRoot, 'worktree', 'add', '--detach', $worktree, 'HEAD') -WorkingDirectory $RepositoryRoot | Out-Null
        $worktreeAdded = $true
        Invoke-NativeCommand -FilePath git -Arguments @('-C', $worktree, 'apply', '--check', '--whitespace=error-all', $patchPath) -WorkingDirectory $worktree | Out-Null
        Invoke-NativeCommand -FilePath git -Arguments @('-C', $worktree, 'apply', '--whitespace=fix', $patchPath) -WorkingDirectory $worktree | Out-Null
        $changedPaths = @(Get-ChangedPaths -Worktree $worktree)
        Assert-ChangedPaths -Paths $changedPaths

        Invoke-NativeCommand -FilePath dotnet -Arguments @('build', '.\PrisonerDiplomacy.csproj', '-c', 'Release', '-t:Rebuild', '--nologo') -WorkingDirectory $worktree -LogPath $buildLogPath | Out-Null
        Invoke-NativeCommand -FilePath pwsh -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', '.\Tools\ValidateLocalization.ps1') -WorkingDirectory $worktree -LogPath $localizationLogPath | Out-Null
        if (-not $SkipSmokeTest) {
            Invoke-SmokeTest -Worktree $worktree -CandidatePaths $changedPaths -SmokeLogPath $smokeLogPath
        }

        if ($ValidationOnly) {
            Write-RepairReport -Path $reportPath -Issue $Issue -Candidate $candidate -CandidateSha $candidateSha -Outcome 'validated locally; no branch or status change' -ChangedPaths $changedPaths
            return [pscustomobject]@{ hash = $hash; outcome = 'validated'; branch = ''; pull_request = ''; report = $reportPath }
        }

        $remoteBranch = @(& git -C $RepositoryRoot ls-remote --heads origin $branch)
        if ($LASTEXITCODE -ne 0) {
            throw 'git ls-remote failed while checking the repair branch'
        }
        if ($remoteBranch.Count -gt 0) {
            throw "repair branch already exists on origin: $branch"
        }
        Invoke-NativeCommand -FilePath git -Arguments @('-C', $worktree, 'switch', '-c', $branch) -WorkingDirectory $worktree | Out-Null
        Invoke-NativeCommand -FilePath git -Arguments @('-C', $worktree, 'add', '--', 'Source/PrisonerDiplomacy', '1.6/Defs', '1.6/Languages', '1.6/Assemblies/PrisonerDiplomacy.dll') -WorkingDirectory $worktree | Out-Null
        Invoke-NativeCommand -FilePath git -Arguments @('-C', $worktree, 'commit', '-m', "Validate telemetry repair $($hash.Substring(0, 12))") -WorkingDirectory $worktree | Out-Null

        Write-RepairReport -Path $reportPath -Issue $Issue -Candidate $candidate -CandidateSha $candidateSha -Outcome 'validated; awaiting human review' -ChangedPaths $changedPaths -Branch $branch
        if ($PublishPullRequest) {
            Invoke-NativeCommand -FilePath git -Arguments @('-C', $worktree, 'push', '-u', 'origin', $branch) -WorkingDirectory $worktree | Out-Null
            Write-PullRequestBody -Path $prBodyPath -Hash $hash -CandidateSha $candidateSha -ChangedPaths $changedPaths
            $prOutput = Invoke-NativeCommand -FilePath gh -Arguments @('pr', 'create', '--base', 'main', '--head', $branch, '--title', "Telemetry repair [$($hash.Substring(0, 12))]", '--body-file', $prBodyPath) -WorkingDirectory $worktree
            $pullRequestUrl = [string]($prOutput | Select-Object -Last 1)
            Write-RepairReport -Path $reportPath -Issue $Issue -Candidate $candidate -CandidateSha $candidateSha -Outcome 'validated; pull request awaiting human review' -ChangedPaths $changedPaths -Branch $branch -PullRequestUrl $pullRequestUrl
        }
        if (-not $script:LocalCandidateMode) {
            Set-IssueStatus -Hash $hash -Status analyzing
        }
        return [pscustomobject]@{ hash = $hash; outcome = 'validated'; branch = $branch; pull_request = $pullRequestUrl; report = $reportPath }
    }
    catch {
        $failure = $_.Exception.Message
        Write-RepairReport -Path $reportPath -Issue $Issue -Candidate $candidate -CandidateSha $candidateSha -Outcome 'needs reproduction or manual repair' -ChangedPaths $changedPaths -Branch $branch -PullRequestUrl $pullRequestUrl -Failure $failure
        if (-not $script:LocalCandidateMode) {
            try {
                Set-IssueStatus -Hash $hash -Status needs_repro
            }
            catch {
                $failure += "`nStatus update also failed: $($_.Exception.Message)"
            }
        }
        return [pscustomobject]@{ hash = $hash; outcome = 'needs_repro'; branch = ''; pull_request = ''; report = $reportPath; error = $failure }
    }
    finally {
        if ($worktreeAdded) {
            & git -C $RepositoryRoot worktree remove --force $worktree 2>$null
        }
        if (Test-Path -LiteralPath $patchPath -PathType Leaf) {
            Remove-Item -LiteralPath $patchPath -Force
        }
        if (Test-Path -LiteralPath $prBodyPath -PathType Leaf) {
            Remove-Item -LiteralPath $prBodyPath -Force
        }
    }
}

function Invoke-SelfTest {
    if (-not (Test-AllowedPatchPath 'Source/PrisonerDiplomacy/Core/Deal.cs')) { throw 'allowed C# path rejected' }
    if (-not (Test-AllowedPatchPath '1.6/Defs/ThingDefs/Test.xml')) { throw 'allowed Def path rejected' }
    if (Test-AllowedPatchPath 'Backend/PrisonerDiplomacyTelemetry/src/index.ts') { throw 'backend path accepted' }
    if (Test-AllowedPatchPath '../outside.cs') { throw 'path traversal accepted' }
    $safePatch = "diff --git a/Source/PrisonerDiplomacy/Core/Test.cs b/Source/PrisonerDiplomacy/Core/Test.cs`n--- a/Source/PrisonerDiplomacy/Core/Test.cs`n+++ b/Source/PrisonerDiplomacy/Core/Test.cs`n@@ -1 +1 @@`n-old`n+new"
    Assert-UnifiedPatch -Patch $safePatch
    try {
        Assert-NoDangerousAdditions -Patch ($safePatch + "`n+Process.Start(`"cmd.exe`")")
        throw 'dangerous addition accepted'
    }
    catch {
        if ($_.Exception.Message -eq 'dangerous addition accepted') { throw }
    }
    $head = [string](& git -C $RepositoryRoot rev-parse HEAD)
    if ($LASTEXITCODE -ne 0) { throw 'could not resolve HEAD during self-test' }
    Assert-SourceRefCompatible -SourceRef $head.Trim()
    $selfTestPrBody = Join-Path ([IO.Path]::GetTempPath()) ("pd-repair-pr-selftest-" + [Guid]::NewGuid().ToString('N') + '.md')
    try {
        Write-PullRequestBody -Path $selfTestPrBody -Hash ('a' * 64) -CandidateSha ('b' * 64) -ChangedPaths @('Source/PrisonerDiplomacy/Core/Test.cs')
        $publicBody = Get-Content -Raw -LiteralPath $selfTestPrBody
        if ($publicBody -notmatch 'Private telemetry samples.+intentionally omitted') {
            throw 'public PR body did not disclose telemetry omission'
        }
        if ($publicBody.Contains(('a' * 64))) {
            throw 'public PR body exposed the full error hash'
        }
    }
    finally {
        if (Test-Path -LiteralPath $selfTestPrBody) {
            Remove-Item -LiteralPath $selfTestPrBody -Force
        }
    }
    Write-Output 'PASS telemetry repair executor self-test'
}

if (-not $RepositoryRoot) {
    $RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
}
else {
    $RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
}
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.git'))) {
    throw "repository root is invalid: $RepositoryRoot"
}
if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}
if (-not $ReportRoot) {
    $ReportRoot = Join-Path $RepositoryRoot 'TelemetryRepairReports'
}
$ReportRoot = [IO.Path]::GetFullPath($ReportRoot)
New-Item -ItemType Directory -Path $ReportRoot -Force | Out-Null

$script:LocalCandidateMode = -not [string]::IsNullOrWhiteSpace($CandidateFile)
if ($script:LocalCandidateMode) {
    $fixture = Get-Content -Raw -LiteralPath $CandidateFile | ConvertFrom-Json -Depth 20
    if ($null -eq $fixture.issue -or $null -eq $fixture.candidate) {
        throw 'candidate fixture must contain issue and candidate objects'
    }
    $fixture.issue | Add-Member -NotePropertyName repair_candidate_json `
        -NotePropertyValue ($fixture.candidate | ConvertTo-Json -Depth 20 -Compress) -Force
    $results = @(Invoke-Candidate -Issue $fixture.issue)
    $results | ConvertTo-Json -Depth 6
    if (@($results | Where-Object { $_.outcome -ne 'validated' }).Count -gt 0) {
        exit 2
    }
    exit 0
}

if (-not $ApiBaseUrl) {
    $ApiBaseUrl = "https://prisoner-diplomacy-telemetry-$Environment.g402111111.workers.dev"
}
$script:ResolvedApiBaseUrl = $ApiBaseUrl.TrimEnd('/')
if (-not $AdminTokenFile) {
    $tokenFileName = if ($Environment -eq 'staging') { '.dev.vars' } else { '.production-admin-token' }
    $AdminTokenFile = Join-Path $RepositoryRoot "Backend\PrisonerDiplomacyTelemetry\$tokenFileName"
}
$script:ResolvedAdminToken = if ($AdminToken) { $AdminToken.Trim() }
elseif ($env:PD_TELEMETRY_ADMIN_TOKEN) { $env:PD_TELEMETRY_ADMIN_TOKEN.Trim() }
elseif (Test-Path -LiteralPath $AdminTokenFile -PathType Leaf) { Read-AdminTokenFile -Path $AdminTokenFile }
else { '' }
if (-not $script:ResolvedAdminToken) {
    throw 'admin token is not available through parameter, environment variable, or token file'
}

$response = Invoke-AdminRequest -Method GET -Path "/api/admin/fix-candidates?limit=$MaximumCandidates"
$issues = @($response.issues)
if ($issues.Count -eq 0) {
    Write-Output "No repair candidates are waiting in $Environment."
    exit 0
}

$results = @($issues | ForEach-Object { Invoke-Candidate -Issue $_ })
$results | ConvertTo-Json -Depth 6
if (@($results | Where-Object { $_.outcome -ne 'validated' }).Count -gt 0) {
    exit 2
}

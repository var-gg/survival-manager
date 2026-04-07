param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [int]$UnityRecoveryBudget = 2
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

$repoRootAbsolute = (Resolve-Path $RepoRoot).Path
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$fullSha = ((git rev-parse HEAD) | Select-Object -First 1).Trim()
$shortSha = ((git rev-parse --short HEAD) | Select-Object -First 1).Trim()
$packetDirectory = Join-Path $repoRootAbsolute "Logs/release-floor/$timestamp-$shortSha"
$manifestPath = Join-Path $packetDirectory 'manifest.json'
$summaryPath = Join-Path $packetDirectory 'summary.md'
$townObserverPath = Join-Path $packetDirectory 'town_observer_contract.json'
$battleObserverPath = Join-Path $packetDirectory 'battle_observer_contract.json'

New-Item -ItemType Directory -Path $packetDirectory -Force | Out-Null

$script:UnityRecoveryCount = 0
$script:PhaseResults = New-Object System.Collections.Generic.List[object]
$script:OverallStatus = 'running'
$script:FailureMessage = ''
$script:ManualChecks = @(
    'clean clone newcomer witness: Unity 6000.4.0f1 -> Prepare Observer Playable -> Boot -> Start Local Run',
    'normal loop manual smoke: first reward return, selector lock, Resume Expedition, boss extract close',
    'Quick Battle (Smoke) manual smoke with campaign progression unchanged',
    'ko/en overlay toggle, battle UI raw key missing 0 확인',
    'Town save/load round-trip 확인',
    'Prepare Observer Playable / Repair First Playable Scenes recovery 확인',
    'tasks/001, tasks/012, tasks/015 evidence refresh 및 summary/status 동기화'
)

function ConvertTo-Iso8601 {
    param(
        [Parameter(Mandatory = $true)]
        [datetime]$Value
    )

    return $Value.ToString('o')
}

function Get-RelativeRepoPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $Path
    }

    $resolved = $Path
    if (Test-Path $Path) {
        $resolved = (Resolve-Path $Path).Path
    }

    if ($resolved.StartsWith($repoRootAbsolute, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $resolved.Substring($repoRootAbsolute.Length).TrimStart('\', '/')
    }

    return $resolved
}

function Save-Manifest {
    $createdAt = ConvertTo-Iso8601 (Get-Date)
    $relativePacketDirectory = Get-RelativeRepoPath $packetDirectory
    $phaseResults = if ($script:PhaseResults.Count -gt 0) { $script:PhaseResults.ToArray() } else { @() }
    $generatedPacket = @(
        (Get-RelativeRepoPath $manifestPath),
        (Get-RelativeRepoPath $summaryPath),
        (Get-RelativeRepoPath $townObserverPath),
        (Get-RelativeRepoPath $battleObserverPath)
    )

    $manifest = @{
        createdAt       = $createdAt
        repoRoot        = $repoRootAbsolute
        git             = @{
            sha      = $fullSha
            shortSha = $shortSha
        }
        packetDirectory = $relativePacketDirectory
        overallStatus   = $script:OverallStatus
        failureMessage  = $script:FailureMessage
        unityRecoveries = @{
            used   = $script:UnityRecoveryCount
            budget = $UnityRecoveryBudget
        }
        phases          = $phaseResults
        manualChecks    = @($script:ManualChecks)
        generatedPacket = $generatedPacket
    }

    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Path $manifestPath -Encoding UTF8
}

function Save-Summary {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Pre-Art RC Summary')
    $lines.Add('')
    $lines.Add("- 상태: $($script:OverallStatus)")
    $lines.Add("- 생성시각: $(ConvertTo-Iso8601 (Get-Date))")
    $lines.Add('- commit SHA: `' + $fullSha + '`')
    $lines.Add('- packet 경로: `' + (Get-RelativeRepoPath $packetDirectory) + '`')
    $lines.Add("- Unity recovery: $($script:UnityRecoveryCount) / $UnityRecoveryBudget")
    if (-not [string]::IsNullOrWhiteSpace($script:FailureMessage)) {
        $lines.Add("- 실패 원인: $($script:FailureMessage)")
    }

    $lines.Add('')
    $lines.Add('## Automated Floors')
    $lines.Add('')
    $lines.Add('| Phase | Status | Command | Artifacts | Notes |')
    $lines.Add('| --- | --- | --- | --- | --- |')

    foreach ($phase in $script:PhaseResults) {
        $artifactText = if ($phase.artifacts.Count -gt 0) { ($phase.artifacts -join '<br>') } else { '-' }
        $noteText = if ([string]::IsNullOrWhiteSpace($phase.note)) { '-' } else { $phase.note }
        $lines.Add('| ' + $phase.name + ' | ' + $phase.status + ' | `' + $phase.command + '` | ' + $artifactText + ' | ' + $noteText + ' |')
    }

    $lines.Add('')
    $lines.Add('## Packet')
    $lines.Add('')
    $lines.Add('- `' + (Get-RelativeRepoPath $manifestPath) + '`')
    $lines.Add('- `' + (Get-RelativeRepoPath $summaryPath) + '`')
    $lines.Add('- `' + (Get-RelativeRepoPath $townObserverPath) + '`')
    $lines.Add('- `' + (Get-RelativeRepoPath $battleObserverPath) + '`')

    $lines.Add('')
    $lines.Add('## Manual Sign-Off Remaining')
    $lines.Add('')
    foreach ($manualCheck in $script:ManualChecks) {
        $lines.Add("- [ ] $manualCheck")
    }

    $lines | Set-Content -Path $summaryPath -Encoding UTF8
}

function Add-PhaseResult {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [Parameter(Mandatory = $true)]
        [datetime]$StartedAt,
        [Parameter(Mandatory = $true)]
        [datetime]$FinishedAt,
        [Parameter(Mandatory = $true)]
        [string]$Status,
        [string]$Note = '',
        [string[]]$Artifacts = @(),
        [int]$AttemptCount = 1
    )

    $phase = [ordered]@{
        name            = $Name
        command         = $Command
        status          = $Status
        startedAt       = ConvertTo-Iso8601 $StartedAt
        finishedAt      = ConvertTo-Iso8601 $FinishedAt
        durationSeconds = [math]::Round(($FinishedAt - $StartedAt).TotalSeconds, 2)
        attempts        = $AttemptCount
        artifacts       = @($Artifacts | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { Get-RelativeRepoPath $_ })
        note            = $Note
    }

    $script:PhaseResults.Add([pscustomobject]$phase)
    Save-Manifest
    Save-Summary
}

function Invoke-PhaseProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @(),
        [switch]$CaptureOutput
    )

    $resolvedFilePath = (Resolve-Path $FilePath).Path
    $output = & pwsh -NoProfile -File $resolvedFilePath @ArgumentList 2>&1
    $outputText = (($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()

    if (-not $CaptureOutput -and $output.Count -gt 0) {
        $output | Write-Output
    }

    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output   = $outputText
    }
}

function Test-UnityBridgeRecoveryMessage {
    param(
        [string]$Text
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $false
    }

    return $Text -match '(?im)(connector remained busy|connection refused|not responding|did not return to ready state|request timed out|context deadline exceeded|connection reset)'
}

function Invoke-UnityRecovery {
    if ($script:UnityRecoveryCount -ge $UnityRecoveryBudget) {
        return $false
    }

    $script:UnityRecoveryCount++
    Write-Host "== unity recovery ($($script:UnityRecoveryCount)/$UnityRecoveryBudget) =="

    $focusResult = Invoke-PhaseProcess -FilePath (Join-Path $PSScriptRoot 'focus-unity.ps1')
    if ($focusResult.ExitCode -ne 0) {
        Write-Host 'Unity focus command did not succeed. Continuing to wait phase.'
    }

    $waitResult = Invoke-PhaseProcess -FilePath (Join-Path $PSScriptRoot 'wait-unity-ready.ps1')
    return $waitResult.ExitCode -eq 0
}

function Invoke-RcPhase {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @(),
        [Parameter(Mandatory = $true)]
        [string]$DisplayCommand,
        [string[]]$Artifacts = @(),
        [switch]$UnityCliPhase,
        [switch]$CaptureOutput
    )

    $attempt = 0

    while ($true) {
        $attempt++
        $startedAt = Get-Date
        Write-Host "== phase: $Name (attempt $attempt) =="
        $result = Invoke-PhaseProcess -FilePath $FilePath -ArgumentList $ArgumentList -CaptureOutput:$CaptureOutput
        $finishedAt = Get-Date

        if ($result.ExitCode -eq 0) {
            $note = if ($CaptureOutput -and -not [string]::IsNullOrWhiteSpace($result.Output)) { 'captured stdout' } else { '' }
            Add-PhaseResult -Name $Name -Command $DisplayCommand -StartedAt $startedAt -FinishedAt $finishedAt -Status 'passed' -Artifacts $Artifacts -AttemptCount $attempt -Note $note
            return $result
        }

        $errorText = $result.Output
        if ($UnityCliPhase -and (Test-UnityBridgeRecoveryMessage -Text $errorText) -and (Invoke-UnityRecovery)) {
            continue
        }

        Add-PhaseResult -Name $Name -Command $DisplayCommand -StartedAt $startedAt -FinishedAt $finishedAt -Status 'failed' -Artifacts $Artifacts -AttemptCount $attempt -Note $errorText
        throw "RC phase failed: $Name"
    }
}

function ConvertFrom-BridgeJsonText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $trimmed = $Text.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        throw 'Bridge output was empty.'
    }

    if ($trimmed.StartsWith('{') -or $trimmed.StartsWith('[')) {
        return $trimmed | ConvertFrom-Json
    }

    $startIndex = $trimmed.IndexOf('{')
    $endIndex = $trimmed.LastIndexOf('}')
    if ($startIndex -ge 0 -and $endIndex -gt $startIndex) {
        return $trimmed.Substring($startIndex, ($endIndex - $startIndex) + 1) | ConvertFrom-Json
    }

    throw 'Bridge output did not contain JSON payload.'
}

try {
    Save-Manifest
    Save-Summary

    [void](Invoke-RcPhase -Name 'docs-policy' -FilePath 'tools/docs-policy-check.ps1' -ArgumentList @('-RepoRoot', '.') -DisplayCommand "pwsh -File tools/docs-policy-check.ps1 -RepoRoot .")
    [void](Invoke-RcPhase -Name 'repo-structure-smoke' -FilePath 'tools/smoke-check.ps1' -ArgumentList @('-RepoRoot', '.') -DisplayCommand "pwsh -File tools/smoke-check.ps1 -RepoRoot .")
    [void](Invoke-RcPhase -Name 'test-harness-lint' -FilePath 'tools/test-harness-lint.ps1' -ArgumentList @('-RepoRoot', '.') -DisplayCommand "pwsh -File tools/test-harness-lint.ps1 -RepoRoot .")

    [void](Invoke-RcPhase -Name 'compile' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('compile') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 compile" -UnityCliPhase)
    Start-Sleep -Seconds 5

    [void](Invoke-RcPhase -Name 'fastunit-batch' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('test-batch-fast') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 test-batch-fast" -Artifacts @('TestResults-Batch.xml'))
    [void](Invoke-RcPhase -Name 'content-validate' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('content-validate') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 content-validate" -Artifacts @('Logs/content-validation-ci.log', 'Logs/content-validation'))
    [void](Invoke-RcPhase -Name 'balance-sweep-smoke' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('balance-sweep-smoke') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 balance-sweep-smoke" -Artifacts @('Logs/balance-sweep-ci.log', 'Logs/balance-sweep'))
    [void](Invoke-RcPhase -Name 'editmode-batch' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('test-batch-edit') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 test-batch-edit" -Artifacts @('TestResults-Batch.xml'))
    [void](Invoke-RcPhase -Name 'playmode' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('test-play') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 test-play" -UnityCliPhase)

    [void](Invoke-RcPhase -Name 'loopd-slice' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('loopd-slice') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 loopd-slice" -UnityCliPhase -Artifacts @('Logs/loop-d-balance/first_playable_slice.md'))
    [void](Invoke-RcPhase -Name 'loopd-purekit' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('loopd-purekit') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 loopd-purekit" -UnityCliPhase -Artifacts @('Logs/loop-d-balance/purekit_report.json'))
    [void](Invoke-RcPhase -Name 'loopd-systemic' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('loopd-systemic') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 loopd-systemic" -UnityCliPhase -Artifacts @('Logs/loop-d-balance/systemic_slice_report.json'))
    [void](Invoke-RcPhase -Name 'loopd-runlite' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('loopd-runlite') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 loopd-runlite" -UnityCliPhase -Artifacts @('Logs/loop-d-balance/runlite_report.json', 'Logs/loop-d-balance/content_health_cards.csv', 'Logs/loop-d-balance/prune_ledger_v1.json', 'Logs/loop-d-balance/readability_watchlist.json', 'Logs/loop-d-balance/loop_d_closure_note.txt'))

    [void](Invoke-RcPhase -Name 'prepare-playable' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('prepare-playable') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 prepare-playable" -UnityCliPhase)
    $townReport = Invoke-RcPhase -Name 'report-town' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('report-town') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 report-town" -UnityCliPhase -CaptureOutput
    $battleReport = Invoke-RcPhase -Name 'report-battle' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('report-battle') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 report-battle" -UnityCliPhase -CaptureOutput

    (ConvertFrom-BridgeJsonText -Text $townReport.Output) | ConvertTo-Json -Depth 8 | Set-Content -Path $townObserverPath -Encoding UTF8
    (ConvertFrom-BridgeJsonText -Text $battleReport.Output) | ConvertTo-Json -Depth 8 | Set-Content -Path $battleObserverPath -Encoding UTF8

    [void](Invoke-RcPhase -Name 'console-snapshot' -FilePath 'tools/unity-bridge.ps1' -ArgumentList @('console', '-Lines', '200', '-Filter', 'error,warning,log') -DisplayCommand "pwsh -File tools/unity-bridge.ps1 console -Lines 200 -Filter error,warning,log" -UnityCliPhase)

    $script:OverallStatus = 'automated_floors_green_manual_signoff_pending'
    Save-Manifest
    Save-Summary
    exit 0
}
catch {
    $script:OverallStatus = 'failed'
    $script:FailureMessage = $_.Exception.Message
    Save-Manifest
    Save-Summary
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}

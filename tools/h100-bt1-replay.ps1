param(
    [int]$SeedBase = 1701,
    [int]$CampaignSiteSafety = 32,
    [int]$MaxBattleSteps = 300,
    [string]$OutputDirectory = 'Logs/h100-bt1-replay'
)

$ErrorActionPreference = 'Stop'
$invariantCulture = [Globalization.CultureInfo]::InvariantCulture
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
}
$captureDirectory = Join-Path $resolvedOutput 'capture'
$captureTracePath = Join-Path $captureDirectory 'sealed-decision-trace-v1.json'
$cultures = @('tr-TR', 'de-DE', 'ja-JP')
$environmentNames = @(
    'SM_H100_SEED_BASE',
    'SM_H100_SITE_SAFETY',
    'SM_H100_MAX_BATTLE_STEPS',
    'SM_H100_SEALED_OUTPUT',
    'SM_H100_TRACE_PATH',
    'SM_H100_REPLAY_RESULT_DIR',
    'SM_H100_FORCE_CULTURE',
    'SM_H100_REPLAY_RESULT_DIRS'
)
$previous = @{}

function Format-InvariantValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return ''
    }
    if ($Value -is [IFormattable]) {
        return $Value.ToString($null, $invariantCulture)
    }
    return $Value.ToString()
}

foreach ($name in $environmentNames) {
    $existing = Get-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
    $previous[$name] = if ($null -eq $existing) {
        [pscustomobject]@{ Exists = $false; Value = $null }
    }
    else {
        [pscustomobject]@{ Exists = $true; Value = $existing.Value }
    }
}

try {
    Set-Item -LiteralPath 'Env:SM_H100_SEED_BASE' -Value $SeedBase.ToString($invariantCulture)
    Set-Item -LiteralPath 'Env:SM_H100_SITE_SAFETY' -Value $CampaignSiteSafety.ToString($invariantCulture)
    Set-Item -LiteralPath 'Env:SM_H100_MAX_BATTLE_STEPS' -Value $MaxBattleSteps.ToString($invariantCulture)
    Set-Item -LiteralPath 'Env:SM_H100_SEALED_OUTPUT' -Value $captureDirectory
    Remove-Item -LiteralPath 'Env:SM_H100_TRACE_PATH' -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath 'Env:SM_H100_REPLAY_RESULT_DIR' -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath 'Env:SM_H100_FORCE_CULTURE' -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath 'Env:SM_H100_REPLAY_RESULT_DIRS' -ErrorAction SilentlyContinue

    & pwsh -File $executeMethod `
        -Method 'SM.Editor.Validation.H100SealedBridgeRunner.RunFromCli' `
        -LogFile 'Logs/h100-bt1-replay-capture.log' `
        -PhaseName 'H100 BT1 sealed capture' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 BT1 capture executeMethod failed with exit code $($LASTEXITCODE.ToString($invariantCulture))."
    }
    if (-not (Test-Path -LiteralPath $captureTracePath -PathType Leaf)) {
        throw "H100 BT1 capture trace missing: $captureTracePath"
    }

    $replayDirectories = @()
    for ($index = 0; $index -lt $cultures.Count; $index++) {
        $replayNumber = $index + 1
        $replayNumberText = $replayNumber.ToString($invariantCulture)
        $replayDirectory = Join-Path $resolvedOutput "replay-$replayNumberText"
        $replayDirectories += $replayDirectory
        Set-Item -LiteralPath 'Env:SM_H100_TRACE_PATH' -Value $captureTracePath
        Set-Item -LiteralPath 'Env:SM_H100_REPLAY_RESULT_DIR' -Value $replayDirectory
        Set-Item -LiteralPath 'Env:SM_H100_FORCE_CULTURE' -Value $cultures[$index]
        Set-Item -LiteralPath 'Env:SM_H100_SEALED_OUTPUT' -Value $replayDirectory

        & pwsh -File $executeMethod `
            -Method 'SM.Editor.Validation.H100SealedBridgeRunner.RunReplayFromCli' `
            -LogFile "Logs/h100-bt1-replay-$replayNumberText.log" `
            -PhaseName "H100 BT1 replay $replayNumberText ($($cultures[$index]))" `
            -ProjectRoot $projectRoot
        if ($LASTEXITCODE -ne 0) {
            throw "H100 BT1 replay $replayNumberText executeMethod failed with exit code $($LASTEXITCODE.ToString($invariantCulture))."
        }

        foreach ($artifactName in 'rebuilt-trace.json', 'replay-env.json') {
            $artifactPath = Join-Path $replayDirectory $artifactName
            if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
                throw "H100 BT1 replay artifact missing: $artifactPath"
            }
        }
    }

    Set-Item -LiteralPath 'Env:SM_H100_TRACE_PATH' -Value $captureTracePath
    Set-Item -LiteralPath 'Env:SM_H100_REPLAY_RESULT_DIRS' -Value ($replayDirectories -join ';')
    Set-Item -LiteralPath 'Env:SM_H100_SEALED_OUTPUT' -Value $resolvedOutput
    Remove-Item -LiteralPath 'Env:SM_H100_REPLAY_RESULT_DIR' -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath 'Env:SM_H100_FORCE_CULTURE' -ErrorAction SilentlyContinue

    & pwsh -File $executeMethod `
        -Method 'SM.Editor.Validation.H100SealedBridgeRunner.RunBt1GateFromCli' `
        -LogFile 'Logs/h100-bt1-replay-gate.log' `
        -PhaseName 'H100 BT1 replay gate aggregation' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 BT1 gate executeMethod failed with exit code $($LASTEXITCODE.ToString($invariantCulture))."
    }

    $gateReportPath = Join-Path $resolvedOutput 'h100-bt1-gate-report.json'
    $witnessPath = Join-Path $resolvedOutput 'bt1-replay-witness.json'
    foreach ($artifactPath in $gateReportPath, $witnessPath) {
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "H100 BT1 gate artifact missing: $artifactPath"
        }
    }

    $gateReport = Get-Content -Raw -LiteralPath $gateReportPath | ConvertFrom-Json
    $bt1Matches = @($gateReport.gates | Where-Object { $_.gate_id -eq 'BT1' })
    if ($bt1Matches.Count -ne 1 -or $bt1Matches[0].status -ne 'pass') {
        throw "H100 BT1 gate did not pass: $($bt1Matches[0].status)"
    }

    $witness = Get-Content -Raw -LiteralPath $witnessPath | ConvertFrom-Json
    if ([int]$witness.independent_process_replay_count -lt 3) {
        throw "H100 BT1 independent process count is below 3: $(Format-InvariantValue $witness.independent_process_replay_count)"
    }
    if ([int]$witness.distinct_applied_culture_count -lt 3) {
        throw "H100 BT1 distinct culture count is below 3: $(Format-InvariantValue $witness.distinct_applied_culture_count)"
    }
    if (-not [bool]$witness.all_byte_identical) {
        throw 'H100 BT1 replay traces are not all byte-identical.'
    }
    if ([double]$witness.state_event_result_hash_match_rate -ne 1.0) {
        throw "H100 BT1 state/event/result hash match rate is not 1.0: $(Format-InvariantValue $witness.state_event_result_hash_match_rate)"
    }
    if ([double]$witness.sealed_llm_decision_trace_replay_match_rate -ne 1.0) {
        throw "H100 BT1 sealed trace replay match rate is not 1.0: $(Format-InvariantValue $witness.sealed_llm_decision_trace_replay_match_rate)"
    }

    Write-Host ([string]::Format(
        $invariantCulture,
        'H100 BT1 replay PASS: count={0} cultures={1} sealed_match={2:R} state_event_match={3:R} report={4}',
        [int]$witness.independent_process_replay_count,
        [int]$witness.distinct_applied_culture_count,
        [double]$witness.sealed_llm_decision_trace_replay_match_rate,
        [double]$witness.state_event_result_hash_match_rate,
        $gateReportPath))
}
finally {
    foreach ($name in $environmentNames) {
        $saved = $previous[$name]
        if ($null -ne $saved -and $saved.Exists) {
            Set-Item -LiteralPath "Env:$name" -Value $saved.Value
        }
        else {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
    }
}

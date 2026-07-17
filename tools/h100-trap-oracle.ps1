param(
    [int]$SeedBase = 1708,
    [ValidateRange(1, 16)]
    [int]$SeedCount = 2,
    [ValidateRange(1, 24)]
    [int]$MedoidCount = 8,
    [ValidateRange(1, 64)]
    [int]$HealthySampleCount = 12,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [string]$OutputDirectory = 'Logs/h100-option-trap'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_TRAP_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRAP_SEED_COUNT = $SeedCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRAP_MEDOID_COUNT = $MedoidCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRAP_HEALTHY_SAMPLE_COUNT = $HealthySampleCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRAP_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRAP_OUTPUT = $OutputDirectory
}
$previous = @{}

try {
    foreach ($entry in $environmentValues.GetEnumerator()) {
        $existing = Get-Item -LiteralPath "Env:$($entry.Key)" -ErrorAction SilentlyContinue
        $previous[$entry.Key] = if ($null -eq $existing) {
            [pscustomobject]@{ Exists = $false; Value = $null }
        }
        else {
            [pscustomobject]@{ Exists = $true; Value = $existing.Value }
        }
        Set-Item -LiteralPath "Env:$($entry.Key)" -Value $entry.Value
    }

    & pwsh -File $executeMethod `
        -Method 'SM.Editor.Validation.H100OptionTrapRunner.RunFromCli' `
        -LogFile 'Logs/h100-option-trap-ci.log' `
        -PhaseName 'H100 BT1-E08 option trap oracle' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 option-trap executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $reportPath = Join-Path $resolvedOutput 'option_trap_report.json'
    $gatePath = Join-Path $resolvedOutput 'h100-bt1-gate-report.json'
    foreach ($path in @($reportPath, $gatePath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "H100 option-trap artifact missing: $path"
        }
    }

    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if ($report.schema_version -ne 'option-trap-report-bt1-v1' -or -not [bool]$report.golden_neutral) {
        throw "Unexpected option-trap report contract: schema=$($report.schema_version), golden_neutral=$($report.golden_neutral)."
    }
    if ([string]::IsNullOrWhiteSpace([string]$report.reproduction_hash) -or [string]$report.reproduction_hash -notmatch '^[0-9a-f]{64}$') {
        throw 'Option-trap reproduction hash is absent or malformed.'
    }
    if ([int]$report.sampling_plan.seed_count -ne $SeedCount `
        -or [int]$report.sampling_plan.medoid_placement_count -ne $MedoidCount `
        -or [int]$report.sampling_plan.healthy_sample_count -gt $HealthySampleCount) {
        throw 'Option-trap sampling plan does not match the requested right-size settings.'
    }

    $gateReport = Get-Content -Raw -LiteralPath $gatePath | ConvertFrom-Json
    $bt9 = $gateReport.gates | Where-Object { $_.gate_id -eq 'BT9' }
    if ($null -eq $bt9 -or -not [bool]$bt9.evaluable_now) {
        throw 'BT9 was not promoted to evaluable_now.'
    }
    foreach ($threshold in @($bt9.thresholds)) {
        if (-not [bool]$threshold.observed -or $threshold.status -eq 'missing') {
            throw "BT9 metric is missing: $($threshold.metric_id)."
        }
    }

    Write-Host "H100 option-trap artifact: $reportPath"
    Write-Host ("Sweep: contracts={0} mechanical={1} flagged={2} confirmed={3} dominant={4} rescued={5}" -f `
        $report.option_contract_count, $report.mechanical_defect_candidate_count, $report.flagged_option_count, `
        $report.confirmed_trap_count, $report.bug_grade_dominant_count, $report.rescued_enabler_count)
    foreach ($candidate in @($report.owner_verdict_queue | Select-Object -First 12)) {
        Write-Host ("  owner_verdict={0} kind={1} reason={2}" -f $candidate.option_id, $candidate.candidate_kind, $candidate.evidence_summary)
    }
    Write-Host "BT9=$($bt9.status) (measured FAIL is reported; no gameplay content or number is changed)."
}
finally {
    foreach ($name in $environmentValues.Keys) {
        $saved = $previous[$name]
        if ($null -ne $saved -and $saved.Exists) {
            Set-Item -LiteralPath "Env:$name" -Value $saved.Value
        }
        else {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
    }
}

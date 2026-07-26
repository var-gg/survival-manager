param(
    [int]$SeedBase = 1701,
    [ValidateRange(1, 128)]
    [int]$CampaignSiteSafety = 32,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [string]$CoverageAnchorId = 'anchor_iron_line',
    [string]$BaselineTrace = 'Logs/20260726-seal-headless-policy/baseline/coverage/intent_trace.jsonl',
    [string]$OutputDirectory = 'Logs/20260726-seal-headless-policy/measurement'
)

$ErrorActionPreference = 'Stop'
$invariantCulture = [Globalization.CultureInfo]::InvariantCulture
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_SEAL_MEASUREMENT_OUTPUT = $OutputDirectory
    SM_H100_SEAL_BASELINE_TRACE = $BaselineTrace
    SM_H100_SEAL_SEED_BASE = $SeedBase.ToString($invariantCulture)
    SM_H100_SEAL_SITE_SAFETY = $CampaignSiteSafety.ToString($invariantCulture)
    SM_H100_SEAL_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString($invariantCulture)
    SM_H100_SEAL_COVERAGE_ANCHOR = $CoverageAnchorId
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
        -Method 'SM.Editor.Validation.H100SealPolicyMeasurementRunner.RunFromCli' `
        -LogFile 'Logs/h100-seal-policy-measurement.log' `
        -PhaseName 'H100 Seal policy full-campaign measurement' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 Seal policy measurement failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $reportPath = Join-Path $resolvedOutput 'seal-policy-measurement.json'
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw "H100 Seal policy measurement report missing: $reportPath"
    }

    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if ($report.schema_version -ne 'h100-seal-policy-measurement-v1') {
        throw "Unexpected Seal policy measurement schema: $($report.schema_version)"
    }
    if (-not [bool]$report.golden.byte_identical) {
        throw 'The no-Seal playthrough golden is not byte-identical.'
    }

    Write-Host (
        "H100 Seal policy measurement PASS: golden=$($report.golden.byte_identical) " +
        "seals=$($report.with_seal.seal_count) echo_delta=$($report.delta.crafting_echo_spent) " +
        "quality_delta=$($report.delta.mean_roll_quality_after) " +
        "outcome_changed=$($report.delta.campaign_outcome_changed) report=$reportPath")
}
finally {
    foreach ($entry in $previous.GetEnumerator()) {
        if ($entry.Value.Exists) {
            Set-Item -LiteralPath "Env:$($entry.Key)" -Value $entry.Value.Value
        }
        else {
            Remove-Item -LiteralPath "Env:$($entry.Key)" -ErrorAction SilentlyContinue
        }
    }
}

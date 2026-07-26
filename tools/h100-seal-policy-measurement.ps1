param(
    [int]$SeedBase = 1701,
    [ValidateRange(32, 32)]
    [int]$SeedCount = 32,
    [ValidateRange(1, 128)]
    [int]$CampaignSiteSafety = 32,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [string]$CoverageAnchorId = 'anchor_iron_line',
    [string]$BaselineTrace = 'Logs/20260726-seal-headless-policy/baseline/coverage/intent_trace.jsonl',
    [string]$Preregistration = "$HOME/.orchestrator/jobs/20260726-seal-prereg-sample/preregistration.md",
    [string]$OutputDirectory = 'Logs/20260726-seal-prereg-sample/measurement'
)

$ErrorActionPreference = 'Stop'
$invariantCulture = [Globalization.CultureInfo]::InvariantCulture
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_SEAL_MEASUREMENT_OUTPUT = $OutputDirectory
    SM_H100_SEAL_BASELINE_TRACE = $BaselineTrace
    SM_H100_SEAL_PREREGISTRATION = $Preregistration
    SM_H100_SEAL_SEED_BASE = $SeedBase.ToString($invariantCulture)
    SM_H100_SEAL_SEED_COUNT = $SeedCount.ToString($invariantCulture)
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
    if ($report.schema_version -ne 'h100-seal-policy-measurement-v2') {
        throw "Unexpected Seal policy measurement schema: $($report.schema_version)"
    }
    if (-not [bool]$report.golden.byte_identical) {
        throw 'The no-Seal playthrough golden is not byte-identical.'
    }
    if ([int]$report.seed_count -ne 32) {
        throw "The frozen preregistration requires 32 seeds; report has $($report.seed_count)."
    }

    Write-Host (
        "H100 Seal policy measurement PASS: golden=$($report.golden.byte_identical) " +
        "supported=$($report.conclusion.supported_hypothesis) " +
        "windows=$($report.census.total_windows) " +
        "h2_ruled_out=$($report.h2_verdict.ruled_out) " +
        "width_probe=$($report.width_probe.ran) report=$reportPath")
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

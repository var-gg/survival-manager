param(
    [ValidateRange(1, 16)]
    [int]$ArrivalsPerPolicySite = 1,
    [ValidateRange(1, 256)]
    [int]$ArrivalSeedAttempts = 32,
    [int]$SeedBase = 1701,
    [ValidateRange(3, 64)]
    [int]$CampaignSiteSafety = 3,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [ValidateRange(0, 495)]
    [int]$OwnedBuildLimit = 0,
    [ValidateRange(1, 8)]
    [int]$MedoidCount = 8,
    [string]$BaselinePolicies = 'random-legal-v1,greedy-v1,competent-doctrine-v1,competent-formation-v1,competent-counter-adaptive-v1,competent-search-planner-v1',
    [string]$OutputDirectory = 'Logs/h100-preview-policy'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_PREVIEW_ARRIVALS_PER_POLICY_SITE = $ArrivalsPerPolicySite.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_PREVIEW_ARRIVAL_SEED_ATTEMPTS = $ArrivalSeedAttempts.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_PREVIEW_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_PREVIEW_SITE_SAFETY = $CampaignSiteSafety.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_PREVIEW_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_PREVIEW_OWNED_BUILD_LIMIT = $OwnedBuildLimit.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_PREVIEW_MEDOID_COUNT = $MedoidCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_PREVIEW_BASELINE_POLICIES = $BaselinePolicies
    SM_H100_PREVIEW_OUTPUT = $OutputDirectory
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
        -Method 'SM.Editor.Validation.H100PreviewPolicyAcceptanceRunner.RunFromCli' `
        -LogFile 'Logs/h100-preview-policy-ci.log' `
        -PhaseName 'H100 BT1-E06 preview policy acceptance' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 preview-policy executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    foreach ($name in 'preview-policy-arrivals.jsonl','preview-policy-sunken-candidates.jsonl','preview-policy-pairs.jsonl','preview-policy-acceptance.json') {
        $path = Join-Path $resolvedOutput $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "H100 preview-policy artifact missing: $path"
        }
        if ((Get-Item -LiteralPath $path).Length -eq 0) {
            throw "H100 preview-policy artifact is empty: $path"
        }
    }

    $reportPath = Join-Path $resolvedOutput 'preview-policy-acceptance.json'
    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if ($report.schema_version -ne 'h100-preview-policy-acceptance-v1') {
        throw "Unexpected preview-policy report schema: $($report.schema_version)"
    }
    if (@($report.held_out).Count -lt 2) {
        throw "Preview-policy acceptance requires at least two held-out sites."
    }
    foreach ($metricId in 'oracle_0_8_blocker_chosen_win_rate','oracle_0_8_blocker_selection_regret') {
        $threshold = $report.bt8_partial.thresholds | Where-Object { $_.metric_id -eq $metricId }
        if ($null -eq $threshold -or -not [bool]$threshold.observed) {
            throw "BT8 partial supplier metric missing: $metricId"
        }
    }

    Write-Host "H100 preview-policy artifact: $reportPath"
    Write-Host "Acceptance status: $($report.status)"
    Write-Host "Sunken chosen=$($report.sunken.chosen_win_rate) oracle=$($report.sunken.same_state_oracle_win_rate) regret=$($report.sunken.selection_regret)"
    foreach ($site in @($report.held_out)) {
        Write-Host "Held-out $($site.site_id): baseline=$($site.baseline_completion_rate) preview=$($site.preview_completion_rate) degradation=$($site.degradation)"
    }
    Write-Host "Unsupported counter=$($report.evidence.unsupported_counter_decision_count); unnecessary full reset=$($report.reset.unnecessary_full_reset_rate)"
    Write-Host 'Measured FAIL remains a report result; only harness or artifact failures fail this tool.'
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

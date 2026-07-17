param(
    [ValidateRange(1, 8)]
    [int]$CompositionCount = 8,
    [ValidateRange(2, 64)]
    [int]$SeedCount = 2,
    [int]$SeedBase = 1701,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [string]$OutputDirectory = 'Logs/h100-tactical-attribution',
    [string]$FormationReport = 'Logs/h100-formation/formation-report.json',
    [string]$IntentTrackReport = 'Logs/h100-intent-track/intent_track_report.json',
    [string]$PreviewPolicyReport = 'Logs/h100-preview-policy/preview-policy-acceptance.json'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_TACTICAL_ATTRIBUTION_COMPOSITION_COUNT = $CompositionCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TACTICAL_ATTRIBUTION_SEED_COUNT = $SeedCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TACTICAL_ATTRIBUTION_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TACTICAL_ATTRIBUTION_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TACTICAL_ATTRIBUTION_OUTPUT = $OutputDirectory
    SM_H100_TACTICAL_ATTRIBUTION_FORMATION_REPORT = $FormationReport
    SM_H100_TACTICAL_ATTRIBUTION_INTENT_REPORT = $IntentTrackReport
    SM_H100_TACTICAL_ATTRIBUTION_PREVIEW_REPORT = $PreviewPolicyReport
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
        -Method 'SM.Editor.Validation.H100TacticalAttributionRunner.RunFromCli' `
        -LogFile 'Logs/h100-tactical-attribution-ci.log' `
        -PhaseName 'H100 BT1-E09 tactical attribution' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 tactical-attribution executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $reportPath = Join-Path $resolvedOutput 'placement_attribution_report.json'
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw "H100 tactical-attribution report missing: $reportPath"
    }

    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if ($report.schema_version -ne 'placement-attribution-report-bt1-e09-v1') {
        throw "Unexpected tactical-attribution schema: $($report.schema_version)"
    }
    if ($report.status -ne 'complete' -or $report.sample.failed_battle_count -ne 0) {
        throw "Tactical-attribution technical failure: status=$($report.status) failed=$($report.sample.failed_battle_count)"
    }
    if ($report.sample.composition_count -ne $CompositionCount) {
        throw "Tactical-attribution composition coverage mismatch: $($report.sample.composition_count)/$CompositionCount"
    }
    if ($report.sample.encounter_family_count -lt 3) {
        throw "Tactical-attribution requires at least three encounter families: $($report.sample.encounter_family_count)"
    }
    if ($report.sample.seed_count -ne $SeedCount) {
        throw "Tactical-attribution seed coverage mismatch: $($report.sample.seed_count)/$SeedCount"
    }
    if (@($report.pro_conditions).Count -ne 4) {
        throw "Tactical-attribution must emit exactly four Pro conditions: $(@($report.pro_conditions).Count)"
    }
    if (@($report.anchor_dominance).Count -ne 6) {
        throw "Tactical-attribution must emit six anchor rows: $(@($report.anchor_dominance).Count)"
    }
    if (@($report.formation_options).Count -ne 5) {
        throw "Tactical-attribution must join all five formation channels: $(@($report.formation_options).Count)"
    }
    if ($report.semantic_swap.feature_invariant_violation_count -ne 0) {
        throw "Semantic-adjacent corpus violated the declared feature invariant: $($report.semantic_swap.feature_invariant_violation_count)"
    }

    Write-Host "H100 tactical-attribution report: $reportPath"
    Write-Host "Battles=$($report.sample.battle_count) pairs=$($report.sample.pair_count) compositions=$($report.sample.composition_count) families=$($report.sample.encounter_family_count) seeds=$($report.sample.seed_count)"
    Write-Host "Verdict=$($report.verdict); material=$($report.components.material_pair_count); tactical share=$($report.components.tactical_share); raw distance+targeting share=$($report.components.raw_distance_targeting_share)"
    foreach ($condition in @($report.pro_conditions)) {
        Write-Host "$($condition.condition_id): triggered=$($condition.triggered) observed=$($condition.observed_value) threshold=$($condition.threshold)"
    }
    Write-Host 'Measured bug/trap candidates are verdicts only; this tool never changes balance/content values.'
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

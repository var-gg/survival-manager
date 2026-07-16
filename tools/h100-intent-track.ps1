param(
    [ValidateRange(1, 64)]
    [int]$SeedCount = 16,
    [int]$SeedBase = 1701,
    [ValidateRange(1, 128)]
    [int]$CampaignSiteSafety = 32,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [ValidateRange(0, 451)]
    [int]$SystemMedoidSampleCount = 8,
    [ValidateSet('deployment', 'reward', 'recruit', 'level_node', 'refit')]
    [string[]]$Levers = @('deployment', 'reward'),
    [string]$OutputDirectory = 'Logs/h100-intent-track'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_TRACK_SEED_COUNT = $SeedCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRACK_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRACK_SITE_SAFETY = $CampaignSiteSafety.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRACK_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRACK_MEDOID_COUNT = $SystemMedoidSampleCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_TRACK_LEVERS = ($Levers | Sort-Object -Unique) -join ','
    SM_H100_TRACK_OUTPUT = $OutputDirectory
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
        -Method 'SM.Editor.Validation.H100IntentTrackRunner.RunFromCli' `
        -LogFile 'Logs/h100-intent-track-ci.log' `
        -PhaseName 'H100 BT1-E05 intent track oracle' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 intent-track executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $required = @(
        'intent_track_report.json',
        'h100-bt1-gate-report.json',
        'concept_catalog_bt1.json',
        'information_surface_audit.json',
        'intent_trace.jsonl'
    )
    foreach ($name in $required) {
        $path = Join-Path $resolvedOutput $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "H100 intent-track artifact missing: $path"
        }
    }

    $reportPath = Join-Path $resolvedOutput 'intent_track_report.json'
    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    if ($report.schema_version -ne 'intent-track-report-bt1-v1') {
        throw "Unexpected intent-track report schema: $($report.schema_version)"
    }
    if ([int]$report.owner_anchor_count -ne 10 -or [int]$report.seed_count -ne $SeedCount) {
        throw "Intent-track owner matrix mismatch: anchors=$($report.owner_anchor_count), seeds=$($report.seed_count)."
    }
    if (@($report.owner_anchor_summaries).Count -ne 10) {
        throw "Intent-track report must contain 10 owner summaries (actual=$(@($report.owner_anchor_summaries).Count))."
    }
    if (@($report.system_medoid_summaries).Count -ne $SystemMedoidSampleCount) {
        throw "Intent-track medoid summary mismatch: $(@($report.system_medoid_summaries).Count)/$SystemMedoidSampleCount."
    }
    $expectedRuns = (10 + $SystemMedoidSampleCount) * $SeedCount
    if (@($report.runs).Count -ne $expectedRuns) {
        throw "Intent-track run matrix mismatch: $(@($report.runs).Count)/$expectedRuns."
    }

    $gateReport = Get-Content -Raw -LiteralPath (Join-Path $resolvedOutput 'h100-bt1-gate-report.json') | ConvertFrom-Json
    foreach ($gateId in @('BT6', 'BT7')) {
        $gate = $gateReport.gates | Where-Object { $_.gate_id -eq $gateId }
        if ($null -eq $gate -or -not [bool]$gate.evaluable_now) {
            throw "$gateId was not promoted to evaluable_now."
        }
        foreach ($threshold in @($gate.thresholds)) {
            if (-not [bool]$threshold.observed -or $threshold.status -eq 'missing') {
                throw "$gateId metric is missing: $($threshold.metric_id)."
            }
        }
    }

    $traceLineCount = @(Get-Content -LiteralPath (Join-Path $resolvedOutput 'intent_trace.jsonl')).Count
    if ($traceLineCount -le 0) {
        throw 'Intent-track trace artifact is empty.'
    }

    Write-Host "H100 intent-track artifact: $reportPath"
    Write-Host "Matrix: owner=10x$SeedCount medoid=$SystemMedoidSampleCount`x$SeedCount runs=$expectedRuns traces=$traceLineCount"
    foreach ($tier in @($report.tier_summaries)) {
        Write-Host ("  tier={0} track={1}/{2} rate={3:N6} lcb95={4:N6} first_p90={5} drought_p90={6} starvation={7:N6}" -f `
            $tier.availability_tier, $tier.track_available_count, $tier.run_count, [double]$tier.track_available_rate, `
            [double]$tier.track_available_lcb95, $tier.first_progress_p90, $tier.agency_drought_p90, [double]$tier.starvation_rate)
    }
    foreach ($anchor in @($report.owner_anchor_summaries)) {
        Write-Host ("  anchor={0} track={1:N6} capture={2:N6} drought_p90={3} starvation={4:N6} pass={5}" -f `
            $anchor.concept_id, [double]$anchor.track_available_rate, [double]$anchor.policy_capture_rate, `
            $anchor.agency_drought_p90, [double]$anchor.starvation_rate, [bool]$anchor.pass)
    }
    $gapSummary = @($report.gap_distribution | ForEach-Object { "$($_.id):$($_.count)" }) -join ','
    Write-Host "Gap distribution: $gapSummary; false_hope_rate=$($report.false_hope_rate)"
    Write-Host "BT6=$($report.bt6_status); BT7=$($report.bt7_status) (measured FAIL is reported, not converted to tool failure)."
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

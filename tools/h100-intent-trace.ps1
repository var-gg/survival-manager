param(
    [ValidateRange(1, 64)]
    [int]$SeedCount = 8,
    [int]$SeedBase = 1701,
    [ValidateRange(1, 128)]
    [int]$CampaignSiteSafety = 2,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [ValidateSet('coverage', 'discovery', 'both')]
    [string]$Lanes = 'both',
    [string]$CoverageAnchorId = 'anchor_iron_line',
    [string]$OutputDirectory = 'Logs/h100-intent-trace'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_INTENT_SEED_COUNT = $SeedCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_INTENT_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_INTENT_SITE_SAFETY = $CampaignSiteSafety.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_INTENT_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_INTENT_LANES = $Lanes
    SM_H100_INTENT_COVERAGE_ANCHOR = $CoverageAnchorId
    SM_H100_INTENT_OUTPUT = $OutputDirectory
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
        -Method 'SM.Editor.Validation.H100IntentTraceRunner.RunFromCli' `
        -LogFile 'Logs/h100-intent-trace-ci.log' `
        -PhaseName 'H100 BT1-E04 intent trace coverage/discovery smoke' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 intent trace executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $laneNames = if ($Lanes -eq 'both') { @('coverage', 'discovery') } else { @($Lanes) }
    foreach ($lane in $laneNames) {
        $laneRoot = Join-Path $resolvedOutput $lane
        $tracePath = Join-Path $laneRoot 'intent_trace.jsonl'
        $summaryPath = Join-Path $laneRoot 'intent_trace_summary.json'
        if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
            throw "H100 intent trace artifact missing: $tracePath"
        }
        if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf)) {
            throw "H100 intent trace summary missing: $summaryPath"
        }

        $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
        if ($summary.schema_version -ne 'intent-trace-summary-bt1-v1') {
            throw "Unexpected intent trace summary schema for $lane`: $($summary.schema_version)"
        }
        if ([int]$summary.missing_trace_count -ne 0 -or [int]$summary.hidden_fact_use_count -ne 0) {
            throw "$lane intent trace failed completeness/no-cheat (missing=$($summary.missing_trace_count), hidden=$($summary.hidden_fact_use_count))."
        }
        if ([int]$summary.campaigns_with_commit -ne $SeedCount) {
            throw "$lane commit_t coverage incomplete (committed=$($summary.campaigns_with_commit), expected=$SeedCount)."
        }

        $traceLineCount = @(Get-Content -LiteralPath $tracePath).Count
        if ($traceLineCount -ne [int]$summary.trace_line_count -or $traceLineCount -le 0) {
            throw "$lane trace line count mismatch (file=$traceLineCount, summary=$($summary.trace_line_count))."
        }

        $commitDistribution = @($summary.commit_decision_indexes | Group-Object | Sort-Object Name |
            ForEach-Object { "$($_.Name):$($_.Count)" }) -join ','
        $reasonDistribution = @($summary.reason_distribution |
            ForEach-Object { "$($_.reason):$($_.count)" }) -join ','
        Write-Host "H100 intent trace $lane`: lines=$traceLineCount commits=$($summary.campaigns_with_commit)/$SeedCount commit_t=[$commitDistribution] reasons=[$reasonDistribution]"
    }
    Write-Host "H100 intent trace artifacts: $resolvedOutput"
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

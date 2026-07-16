param(
    [ValidateRange(1, 1000)]
    [int]$CampaignCount = 8,
    [int]$SeedBase = 1701,
    [ValidateRange(1, 128)]
    [int]$CampaignSiteSafety = 32,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [string]$OutputDirectory = 'Logs/h100-policy-witness'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_BATTLE_COUNT = '1'
    SM_H100_CAMPAIGN_COUNT = $CampaignCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_REPLAY_COPIES = '2'
    SM_H100_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SITE_SAFETY = $CampaignSiteSafety.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_WRITE_CSV = 'false'
    SM_H100_OUTPUT = $OutputDirectory
    SM_H100_POLICY = 'greedy-v1'
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
        -Method 'SM.Editor.Validation.H100PolicyWitnessRunner.RunFromCli' `
        -LogFile 'Logs/h100-policy-witness-ci.log' `
        -PhaseName 'H100 stage 2 policy witness' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 policy witness executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $resultPath = Join-Path $resolvedOutput 'policy-witness.json'
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "H100 policy witness artifact missing: $resultPath"
    }

    $result = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
    if (-not $result.improved) {
        throw 'SearchPlanner did not exceed Greedy completion or battle win rate.'
    }

    Write-Host "H100 policy witness: $resultPath"
    Write-Host "Completion rate greedy=$($result.greedy_completion_rate) planner=$($result.planner_completion_rate)"
    Write-Host "Battle win rate greedy=$($result.greedy_battle_win_rate) planner=$($result.planner_battle_win_rate)"
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

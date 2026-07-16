param(
    [ValidateRange(1, 64)]
    [int]$CampaignsPerPolicy = 1,
    [ValidateRange(1, 256)]
    [int]$ArrivalSeedAttempts = 32,
    [int]$SeedBase = 1701,
    [ValidateRange(3, 64)]
    [int]$CampaignSiteSafety = 3,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [ValidateRange(0, 495)]
    [int]$OwnedBuildLimit = 0,
    [ValidateRange(1, 495)]
    [int]$LookbackBuildLimit = 12,
    [ValidateRange(1, 8)]
    [int]$MedoidCount = 8,
    [string]$Policies = 'random-legal-v1,greedy-v1,competent-doctrine-v1,competent-formation-v1,competent-counter-adaptive-v1,competent-search-planner-v1',
    [string]$OutputDirectory = 'Logs/h100-sunken-diagnosis'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_SUNKEN_CAMPAIGNS_PER_POLICY = $CampaignsPerPolicy.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SUNKEN_ARRIVAL_SEED_ATTEMPTS = $ArrivalSeedAttempts.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SUNKEN_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SUNKEN_SITE_SAFETY = $CampaignSiteSafety.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SUNKEN_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SUNKEN_OWNED_BUILD_LIMIT = $OwnedBuildLimit.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SUNKEN_LOOKBACK_BUILD_LIMIT = $LookbackBuildLimit.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SUNKEN_MEDOID_COUNT = $MedoidCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SUNKEN_POLICIES = $Policies
    SM_H100_SUNKEN_OUTPUT = $OutputDirectory
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
        -Method 'SM.Editor.Validation.H100SunkenDiagnosisRunner.RunFromCli' `
        -LogFile 'Logs/h100-sunken-diagnosis-ci.log' `
        -PhaseName 'H100 stage 5 sunken diagnosis' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 sunken diagnosis executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    foreach ($name in 'arrival-snapshots.jsonl','oracle-candidates.jsonl','sunken-diagnosis.json') {
        $path = Join-Path $resolvedOutput $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "H100 sunken diagnosis artifact missing: $path"
        }
    }

    $snapshots = @(Get-Content -LiteralPath (Join-Path $resolvedOutput 'arrival-snapshots.jsonl'))
    $candidates = @(Get-Content -LiteralPath (Join-Path $resolvedOutput 'oracle-candidates.jsonl'))
    if ($snapshots.Count -eq 0 -or $candidates.Count -eq 0) {
        throw 'Sunken diagnosis JSONL artifacts must be non-empty.'
    }

    $report = Get-Content -Raw -LiteralPath (Join-Path $resolvedOutput 'sunken-diagnosis.json') | ConvertFrom-Json
    foreach ($property in 'same_state_oracle_win_rate','selection_regret','availability_gap','one_site_lookback_oracle','best_counter_family','decision_cell') {
        if ($report.PSObject.Properties.Name -notcontains $property) {
            throw "Sunken diagnosis report missing property: $property"
        }
    }

    Write-Host "H100 sunken diagnosis artifacts: $resolvedOutput"
    Write-Host "same_state_oracle_win_rate: $($report.same_state_oracle_win_rate)"
    Write-Host "selection_regret: $($report.selection_regret)"
    Write-Host "availability_gap: $($report.availability_gap)"
    Write-Host "one_site_lookback_oracle: $($report.one_site_lookback_oracle)"
    Write-Host "best_counter_family: $($report.best_counter_family)"
    Write-Host "Pro decision cell: $($report.decision_cell)"
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

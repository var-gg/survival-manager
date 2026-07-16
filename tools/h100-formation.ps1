param(
    [ValidateRange(1, 64)]
    [int]$SeedCount = 5,
    [int]$SeedBase = 1701,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [ValidateSet('competent-doctrine-v1', 'competent-formation-v1', 'competent-counter-adaptive-v1', 'competent-search-planner-v1')]
    [string]$CompetentPolicy = 'competent-formation-v1',
    [string]$OutputDirectory = 'Logs/h100-formation'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_FORMATION_SEED_COUNT = $SeedCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_FORMATION_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_FORMATION_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_FORMATION_COMPETENT_POLICY = $CompetentPolicy
    SM_H100_FORMATION_OUTPUT = $OutputDirectory
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
        -Method 'SM.Editor.Validation.H100FormationRunner.RunFromCli' `
        -LogFile 'Logs/h100-formation-ci.log' `
        -PhaseName "H100 stage 4 formation ($CompetentPolicy)" `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 formation executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $required = @(
        'formation-events.jsonl',
        'placement-leverage.jsonl',
        'healer-marginal-value.jsonl',
        'formation-report.json'
    )
    foreach ($name in $required) {
        $path = Join-Path $resolvedOutput $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "H100 formation artifact missing: $path"
        }
    }

    $events = Get-Content -LiteralPath (Join-Path $resolvedOutput 'formation-events.jsonl') |
        ForEach-Object { $_ | ConvertFrom-Json }
    if ($events.Count -eq 0) {
        throw 'formation-events.jsonl is empty.'
    }
    foreach ($property in 'eligible','fired','causal','legible') {
        if ($events[0].PSObject.Properties.Name -notcontains $property) {
            throw "formation event contract missing property: $property"
        }
    }
    $report = Get-Content -Raw -LiteralPath (Join-Path $resolvedOutput 'formation-report.json') | ConvertFrom-Json
    if ($null -eq $report.placement -or $null -eq $report.healer) {
        throw 'formation report must emit placement leverage and healer marginal-value summaries.'
    }

    Write-Host "H100 formation artifacts: $resolvedOutput (competent=$CompetentPolicy)"
    Write-Host "CoveragePolicy coverage pass: $($report.coverage_pass)"
    Write-Host "Competent prevalence pass: $($report.competent_prevalence_pass); impact pass: $($report.competent_impact_pass)"
    Write-Host "Placement pass: $($report.placement_leverage_pass); healer selection pass: $($report.healer_selection_pass)"
    Write-Host "Competent Q5 pass: $($report.competent_q5_pass); Stage 5 balance flag: $($report.needs_stage_five_balance)"
    if ($report.channels_needing_tuning.Count -gt 0) {
        Write-Host "Channels needing tuning: $($report.channels_needing_tuning -join ', ')"
    }
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

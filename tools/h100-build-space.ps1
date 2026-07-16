param(
    [ValidateRange(1, 12)]
    [int]$ScreeningBuildCount = 3,
    [ValidateRange(1, 16)]
    [int]$ScreeningSeedCount = 2,
    [int]$SeedBase = 1701,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [string]$OutputDirectory = 'Logs/h100-build-space'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_CENSUS_BUILD_COUNT = $ScreeningBuildCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_CENSUS_SEED_COUNT = $ScreeningSeedCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_CENSUS_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_CENSUS_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_CENSUS_OUTPUT = $OutputDirectory
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
        -Method 'SM.Editor.Validation.H100BuildSpaceCensusRunner.RunFromCli' `
        -LogFile 'Logs/h100-build-space-ci.log' `
        -PhaseName 'H100 stage 3 build-space census' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 build-space census executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $required = @(
        'build-space.csv',
        'formation-space.csv',
        'formation-medoids.csv',
        'census-report.json',
        'screening-smoke.jsonl',
        'screening-smoke-summary.json'
    )
    foreach ($name in $required) {
        $path = Join-Path $resolvedOutput $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "H100 census artifact missing: $path"
        }
    }

    $report = Get-Content -Raw -LiteralPath (Join-Path $resolvedOutput 'census-report.json') | ConvertFrom-Json
    $expected = [ordered]@{
        total_combinations = 495
        formation_placements_per_combination = 360
        total_states = 178200
        race_tier2_build_count = 495
        class_tier2_build_count = 414
        class_tier3_build_count = 36
        race_tier4_build_count = 3
        upper_doctrine_build_count = 39
        exact_three_race_build_count = 96
        race_two_plus_two_build_count = 108
        class_two_plus_two_build_count = 54
        role_complete_build_count = 81
        medoid_count = 8
    }
    foreach ($entry in $expected.GetEnumerator()) {
        $actual = $report.summary.($entry.Key)
        if ([int]$actual -ne [int]$entry.Value) {
            throw "H100 census assertion failed: $($entry.Key) expected=$($entry.Value) actual=$actual"
        }
    }

    if ((Get-Content -LiteralPath (Join-Path $resolvedOutput 'build-space.csv')).Count -ne 496) {
        throw 'build-space.csv must contain one header plus 495 build rows.'
    }
    if ((Get-Content -LiteralPath (Join-Path $resolvedOutput 'formation-space.csv')).Count -ne 361) {
        throw 'formation-space.csv must contain one header plus 360 placement rows.'
    }
    if ((Get-Content -LiteralPath (Join-Path $resolvedOutput 'formation-medoids.csv')).Count -ne 9) {
        throw 'formation-medoids.csv must contain one header plus 8 medoid rows.'
    }

    $screening = Get-Content -Raw -LiteralPath (Join-Path $resolvedOutput 'screening-smoke-summary.json') | ConvertFrom-Json
    $expectedRecords = 8 * $ScreeningBuildCount * $ScreeningSeedCount
    if ([int]$screening.record_count -ne $expectedRecords) {
        throw "Screening smoke record count mismatch: expected=$expectedRecords actual=$($screening.record_count)"
    }
    if ([int]$screening.failure_count -ne 0 -or [int]$screening.crash_count -ne 0) {
        throw "Screening smoke contains failures: failures=$($screening.failure_count) crashes=$($screening.crash_count)"
    }

    Write-Host "H100 build-space artifacts: $resolvedOutput"
    Write-Host 'Census assertions: builds=495 placements=360 states=178200 class@3=36 race@4=3 race@2=495 race3=96 roles=81'
    Write-Host "Automatic medoids: $($report.summary.medoid_count); screening records: $($screening.record_count)"
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

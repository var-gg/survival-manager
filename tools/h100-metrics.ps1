param(
    [ValidateRange(1, 10000000)]
    [int]$BattleCount = 4,
    [ValidateRange(1, 1000000)]
    [int]$CampaignCount = 1,
    [ValidateRange(2, 16)]
    [int]$ReplayCopies = 2,
    [int]$SeedBase = 1701,
    [ValidateRange(1, 128)]
    [int]$CampaignSiteSafety = 2,
    [ValidateRange(1, 1000000)]
    [int]$MaxBattleSteps = 300,
    [ValidateSet('random-legal-v1', 'greedy-v1', 'competent-doctrine-v1', 'competent-formation-v1', 'competent-counter-adaptive-v1', 'competent-search-planner-v1')]
    [string]$Policy = 'greedy-v1',
    [string]$OutputDirectory = 'Logs/h100-metrics',
    [switch]$NoCsv
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$environmentValues = [ordered]@{
    SM_H100_BATTLE_COUNT = $BattleCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_CAMPAIGN_COUNT = $CampaignCount.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_REPLAY_COPIES = $ReplayCopies.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SEED_BASE = $SeedBase.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_SITE_SAFETY = $CampaignSiteSafety.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_MAX_BATTLE_STEPS = $MaxBattleSteps.ToString([Globalization.CultureInfo]::InvariantCulture)
    SM_H100_POLICY = $Policy
    SM_H100_WRITE_CSV = (-not $NoCsv).ToString().ToLowerInvariant()
    SM_H100_OUTPUT = $OutputDirectory
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
        -Method 'SM.Editor.Validation.H100MetricsRunner.RunFromCli' `
        -LogFile 'Logs/h100-metrics-ci.log' `
        -PhaseName "H100 stage 2 metrics ($Policy)" `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 metrics executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $required = @('battle-metrics.jsonl', 'campaign-metrics.jsonl', 'gate-report.json', 'run-manifest.json')
    foreach ($name in $required) {
        $path = Join-Path $resolvedOutput $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "H100 artifact missing: $path"
        }
    }

    $gateReport = Get-Content -Raw -LiteralPath (Join-Path $resolvedOutput 'gate-report.json') | ConvertFrom-Json
    $integrityGate = $gateReport.gates | Where-Object { $_.gate_id -eq 'integrity_reproducibility' }
    $hashThreshold = $integrityGate.thresholds | Where-Object { $_.metric_id -eq 'replay_hash_match_rate' }
    if (-not $hashThreshold.observed -or [double]$hashThreshold.observed_value -ne 1.0) {
        throw 'H100 replay hash same-seed witness did not reach 100%.'
    }

    Write-Host "H100 metrics artifacts: $resolvedOutput (policy=$Policy)"
    Write-Host "Replay hash match rate: $($hashThreshold.observed_value) (groups=$($hashThreshold.sample_count))"
    Write-Host "Overall H100 gate pass: $($gateReport.overall_pass) (smoke/sample-floor failures are expected for small N)"
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

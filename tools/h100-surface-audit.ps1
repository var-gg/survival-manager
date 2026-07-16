param(
    [string]$OutputDirectory = 'Logs/h100-surface-audit'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$previous = Get-Item -LiteralPath 'Env:SM_H100_SURFACE_OUTPUT' -ErrorAction SilentlyContinue

try {
    Set-Item -LiteralPath 'Env:SM_H100_SURFACE_OUTPUT' -Value $OutputDirectory
    & pwsh -File $executeMethod `
        -Method 'SM.Editor.Validation.H100SurfaceAuditRunner.RunFromCli' `
        -LogFile 'Logs/h100-surface-audit-ci.log' `
        -PhaseName 'H100 E02 information-surface audit' `
        -ProjectRoot $projectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "H100 surface audit executeMethod failed with exit code $LASTEXITCODE."
    }

    $resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
        [IO.Path]::GetFullPath($OutputDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
    }
    $artifactPath = Join-Path $resolvedOutput 'information_surface_audit.json'
    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "H100 surface audit artifact missing: $artifactPath"
    }

    $audit = Get-Content -Raw -LiteralPath $artifactPath | ConvertFrom-Json
    $metricNames = @(
        'actionable_offer_missing_semantics',
        'undefined_visible_token',
        'hidden_prerequisite',
        'description_behavior_mismatch_count'
    )
    foreach ($metricName in $metricNames) {
        if ($null -eq $audit.PSObject.Properties[$metricName]) {
            throw "H100 surface audit metric missing: $metricName"
        }
    }

    $hardPass = ($metricNames | Where-Object { [int]$audit.$_ -ne 0 }).Count -eq 0
    $feedbackPass = [double]$audit.interaction_feedback_coverage -ge 0.90
    $metricSummary = ($metricNames | ForEach-Object { "$_=$($audit.$_)" }) -join ', '
    Write-Host "H100 surface audit artifact: $artifactPath"
    Write-Host "BT3 metrics: $metricSummary (pass=$($hardPass.ToString().ToLowerInvariant()))"
    Write-Host "Interaction feedback coverage: $($audit.interaction_feedback_coverage) (target>=0.90; pass=$($feedbackPass.ToString().ToLowerInvariant()))"
    Write-Host "Reported gaps: $(@($audit.gaps).Count) (content is never auto-modified)"
}
finally {
    if ($null -ne $previous) {
        Set-Item -LiteralPath 'Env:SM_H100_SURFACE_OUTPUT' -Value $previous.Value
    }
    else {
        Remove-Item -LiteralPath 'Env:SM_H100_SURFACE_OUTPUT' -ErrorAction SilentlyContinue
    }
}

param(
    [string]$RepoRoot = "."
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

$requiredPaths = @(
    'AGENTS.md',
    'docs/00_governance/agent-operating-model.md',
    'docs/00_governance/docs-harness.md',
    'docs/00_governance/task-execution-pattern.md',
    'Assets/_Game',
    'Assets/Tests/EditMode',
    'Assets/ThirdParty',
    'Packages/manifest.json',
    'tools/docs-policy-check.ps1'
)

$missing = @()
foreach ($path in $requiredPaths) {
    if (-not (Test-Path $path)) {
        $missing += $path
    }
}

if ($missing.Count -gt 0) {
    Write-Error ("Missing required paths:`n- " + ($missing -join "`n- "))
    exit 1
}

Write-Host 'Smoke check passed.'

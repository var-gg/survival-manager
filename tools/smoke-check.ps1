<#
.SYNOPSIS
저장소 구조 preflight smoke를 확인한다.

.DESCRIPTION
이 스크립트는 runtime playable smoke가 아니라, 문서/도구/에셋의 핵심 경로가
여전히 존재하는지 확인하는 가벼운 repo structure sanity check다.
#>

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
    'Assets/Tests/EditMode/FastUnit',
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

Write-Host 'Repo structure smoke check passed.'

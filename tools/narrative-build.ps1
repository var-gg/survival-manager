#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Narrative 전체 빌드 파이프라인.
.DESCRIPTION
    MD authoring → JSON manifest → Unity SO/Localization → Portrait placeholder 까지
    한 번에 실행한다.

    단계:
    1. Python parser: master-script.md + schema → narrative-seed.json
    2. Unity importer: JSON → StoryEvent/DialogueSequence SO + Content_Story 로컬라이제이션
    3. Portrait placeholder: 캐릭터별 Default.png 자동 생성
.PARAMETER ParseOnly
    Python 파서만 실행하고 Unity import는 건너뛴다.
.PARAMETER SkipPortrait
    Portrait placeholder 생성을 건너뛴다.
#>
param(
    [switch]$ParseOnly,
    [switch]$SkipPortrait
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# --- Step 1: Python parser ---
Write-Host '=== [1/3] Narrative parser ===' -ForegroundColor Cyan

$tempDir = Join-Path $repoRoot 'Temp' 'Narrative'
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
}

$pythonScript = Join-Path $PSScriptRoot 'narrative_build.py'
if (-not (Test-Path $pythonScript)) {
    Write-Error "narrative_build.py not found at $pythonScript"
    exit 1
}

python $pythonScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "Python parser failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

if ($ParseOnly) {
    Write-Host 'ParseOnly mode. Skipping Unity import.' -ForegroundColor Yellow
    exit 0
}

# --- Step 2: Unity importer ---
Write-Host '=== [2/3] Unity SO importer ===' -ForegroundColor Cyan

$executeMethodScript = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
if (-not (Test-Path $executeMethodScript)) {
    Write-Error "unity-execute-method.ps1 not found"
    exit 1
}

& pwsh -File $executeMethodScript `
    -Method 'SM.Editor.Narrative.NarrativeSeedImporter.ImportFromMenu' `
    -LogFile 'Logs/narrative-import.log' `
    -PhaseName 'narrative seed import' `
    -ProjectRoot $repoRoot

if ($LASTEXITCODE -ne 0) {
    Write-Error "Unity narrative import failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# --- Step 3: Portrait placeholders ---
if (-not $SkipPortrait) {
    Write-Host '=== [3/3] Portrait placeholders ===' -ForegroundColor Cyan

    & pwsh -File $executeMethodScript `
        -Method 'SM.Editor.Narrative.NarrativePortraitPlaceholderGenerator.GeneratePlaceholders' `
        -LogFile 'Logs/narrative-portraits.log' `
        -PhaseName 'portrait placeholder generation' `
        -ProjectRoot $repoRoot

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Portrait placeholder generation failed (non-fatal)"
    }
} else {
    Write-Host '=== [3/3] Portrait placeholders (skipped) ===' -ForegroundColor Yellow
}

Write-Host '=== Narrative build complete ===' -ForegroundColor Green
exit 0

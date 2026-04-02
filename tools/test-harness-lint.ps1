param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent)
)

<#
.SYNOPSIS
    테스트 하네스 preflight lint — CI 또는 로컬에서 커밋 전 검증.

.DESCRIPTION
    아래 세 가지를 검사한다:
    1. Runtime asmdef가 UnityEditor를 #if 가드 없이 참조하면 실패
    2. [Category("BatchOnly")]가 아닌 테스트 코드에서 Resources.LoadAll을 호출하면 실패
    3. 스크립트/문서에서 -quit를 -runTests와 같이 사용하면 실패
#>

$ErrorActionPreference = 'Continue'
$exitCode = 0

function Write-LintError {
    param([string]$Check, [string]$File, [string]$Detail)
    Write-Host "LINT FAIL [$Check] $File" -ForegroundColor Red
    Write-Host "  $Detail" -ForegroundColor Yellow
    $script:exitCode = 1
}

# ────────────────────────────────────────────────
# Check 1: Runtime asmdef 소스에서 UnityEditor 참조 (#if 가드 없이)
# ────────────────────────────────────────────────

Write-Host "`n== Check 1: UnityEditor in runtime assemblies ==" -ForegroundColor Cyan

$runtimeDirs = @(
    'Assets/_Game/Scripts/Runtime/Core',
    'Assets/_Game/Scripts/Runtime/Content',
    'Assets/_Game/Scripts/Runtime/Combat',
    'Assets/_Game/Scripts/Runtime/Meta',
    'Assets/_Game/Scripts/Runtime/Unity'
)

foreach ($dir in $runtimeDirs) {
    $fullDir = Join-Path $RepoRoot $dir
    if (-not (Test-Path $fullDir)) { continue }

    $csFiles = Get-ChildItem $fullDir -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $csFiles) {
        $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
        $inEditorGuard = $false
        $lineNum = 0

        foreach ($line in $lines) {
            $lineNum++
            $trimmed = $line.Trim()

            if ($trimmed -match '^#if\s+UNITY_EDITOR') {
                $inEditorGuard = $true
                continue
            }
            if ($trimmed -match '^#endif' -and $inEditorGuard) {
                $inEditorGuard = $false
                continue
            }

            # using UnityEditor outside #if UNITY_EDITOR guard
            if (-not $inEditorGuard -and $trimmed -match '^\s*using\s+UnityEditor') {
                $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
                Write-LintError -Check 'UnityEditor-in-runtime' -File "$relPath`:$lineNum" -Detail "using UnityEditor without #if UNITY_EDITOR guard"
            }
        }
    }
}

if ($exitCode -eq 0) {
    Write-Host "  PASS: No unguarded UnityEditor references in runtime assemblies." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 2: Resources.LoadAll outside BatchOnly tests
# ────────────────────────────────────────────────

Write-Host "`n== Check 2: Resources.LoadAll outside BatchOnly ==" -ForegroundColor Cyan
$check2Fail = $false

$testDir = Join-Path $RepoRoot 'Assets/Tests/EditMode'
if (Test-Path $testDir) {
    $testFiles = Get-ChildItem $testDir -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $testFiles) {
        # 줄 단위로 읽어서 주석이 아닌 코드 라인만 검사
        $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
        if ($null -eq $lines) { continue }

        $codeContent = ($lines | Where-Object {
            $trimmed = $_.TrimStart()
            -not $trimmed.StartsWith('//') -and -not $trimmed.StartsWith('*') -and -not $trimmed.StartsWith('///') -and -not $trimmed.StartsWith('/*')
        }) -join [Environment]::NewLine

        $fullContent = Get-Content $file.FullName -Raw

        # Resources.LoadAll을 코드에서 직접 사용하는 경우
        if ($codeContent -match 'Resources\.LoadAll') {
            if ($fullContent -notmatch '\[Category\(\s*"BatchOnly"\s*\)\]') {
                $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
                Write-LintError -Check 'LoadAll-outside-BatchOnly' -File $relPath -Detail "Resources.LoadAll found but [Category(`"BatchOnly`")] missing on class"
                $check2Fail = $true
            }
        }

        # new RuntimeCombatContentLookup()를 코드에서 직접 생성하는 경우
        if ($codeContent -match 'new\s+RuntimeCombatContentLookup\s*\(') {
            if ($fullContent -notmatch '\[Category\(\s*"BatchOnly"\s*\)\]') {
                $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
                Write-LintError -Check 'RuntimeLookup-outside-BatchOnly' -File $relPath -Detail "new RuntimeCombatContentLookup() found but [Category(`"BatchOnly`")] missing — use FakeCombatContentLookup or add [Category(`"BatchOnly`")]"
                $check2Fail = $true
            }
        }
    }
}

if (-not $check2Fail -and $exitCode -eq 0) {
    Write-Host "  PASS: No Resources.LoadAll usage outside BatchOnly tests." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 3: -quit combined with -runTests
# ────────────────────────────────────────────────

Write-Host "`n== Check 3: -quit with -runTests ==" -ForegroundColor Cyan
$check3Fail = $false

$scriptDirs = @('tools', '.github', '.codex')
$scriptExtensions = @('*.ps1', '*.sh', '*.yml', '*.yaml')

foreach ($dir in $scriptDirs) {
    $fullDir = Join-Path $RepoRoot $dir
    if (-not (Test-Path $fullDir)) { continue }

    foreach ($ext in $scriptExtensions) {
        $files = Get-ChildItem $fullDir -Filter $ext -Recurse -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            # lint 스크립트 자체는 패턴 문자열을 포함하므로 제외
            if ($file.Name -eq 'test-harness-lint.ps1') { continue }
            $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
            if ($null -eq $lines) { continue }

            # 주석이 아닌 코드 라인에서만 -quit과 -runTests 동시 사용을 검사
            $codeLines = $lines | Where-Object {
                $trimmed = $_.TrimStart()
                -not $trimmed.StartsWith('#') -and -not $trimmed.StartsWith('//') -and -not $trimmed.StartsWith('REM')
            }
            $codeContent = $codeLines -join ' '

            # 같은 코드 블록에서 -runTests와 -quit 모두 사용
            if ($codeContent -match '-runTests' -and $codeContent -match "'-quit'|`"-quit`"|\s-quit\b") {
                $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
                Write-LintError -Check 'quit-with-runTests' -File $relPath -Detail "-quit and -runTests found in executable code — -quit can terminate Unity before tests finish"
                $check3Fail = $true
            }
        }
    }
}

if (-not $check3Fail -and $exitCode -eq 0) {
    Write-Host "  PASS: No -quit combined with -runTests." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Summary
# ────────────────────────────────────────────────

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "All test harness lint checks passed." -ForegroundColor Green
}
else {
    Write-Host "Test harness lint checks failed. Fix the issues above before committing." -ForegroundColor Red
}

exit $exitCode

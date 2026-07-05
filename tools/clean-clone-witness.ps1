#Requires -Version 7
<#
clean-clone-witness.ps1 — 릴리스 floor 게이트: 커밋된 트리만으로(fresh clone, Library 제로)
컴파일 → 콘텐츠 검증 → 밸런스 sweep 스모크 → EditMode 테스트가 통과하는지 검증한다.

목적:
  - "내 머신에서만 도는 상태"(미커밋 파일·로컬 캐시 의존·에셋 누락)를 잡는 신규 클론 witness.
  - CI UNITY_LICENSE 게이트가 열리기 전까지의 로컬 보완 게이트(.github/workflows 참고).

사용:
  pwsh -File tools/clean-clone-witness.ps1                    # main HEAD 검증
  pwsh -File tools/clean-clone-witness.ps1 -Ref feature-x
  pwsh -File tools/clean-clone-witness.ps1 -IncludePlayMode -KeepClone

특성:
  - 클론은 별도 Library/lock이므로 메인 에디터가 열려 있어도 실행 가능(CPU 경합만 존재).
  - 첫 phase(콘텐츠 검증)가 전체 에셋 import를 포함해 가장 오래 걸린다(수십 분 단위).
  - 실패 시 클론을 남겨 로그를 보존한다. 성공 시 -KeepClone 없으면 정리.
#>
param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$Ref = 'main',
    [string]$CloneDir,
    [switch]$SkipBalanceSweep,
    [switch]$IncludePlayMode,
    [switch]$KeepClone,
    # 커밋된 트리만으로(에셋팩 없이) 돌리는 strict 모드. 유료팩 의존 테스트가 실패하는 게 정상.
    [switch]$NoMachineOnlyAssetCopy,
    [int]$ImportPhaseTimeoutMinutes = 240,
    [int]$PhaseTimeoutMinutes = 90,
    [int]$MinFreeDiskGb = 30
)

# gitignore로 이 머신에만 존재하는 에셋(.gitignore 75-96행과 동기 유지).
# 신규 머신의 실제 셋업 절차 = clone + 유료팩 임포트 + 승격 아트 복원이므로,
# witness도 기본값으로 이 디렉터리들을 원본 저장소에서 클론으로 복사해 그 절차를 등가 재현한다.
$script:MachineOnlyAssetDirs = @(
    'Assets/Allsky',
    'Assets/Epic Toon FX',
    'Assets/Kevin Iglesias',
    'Assets/TriForge Assets',
    'Assets/P09_Modular_Humanoid',
    'Assets/Quibli',
    'Assets/MagicaCloth2',
    'Assets/Resources/_Game/Art'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $RepoRoot 'ProjectSettings/ProjectVersion.txt'))) {
    throw "RepoRoot가 Unity 프로젝트가 아니다: $RepoRoot"
}
if ([string]::IsNullOrWhiteSpace($CloneDir)) {
    $CloneDir = Join-Path (Split-Path $RepoRoot -Parent) '.sm-clean-clone-witness'
}

function Resolve-UnityEditorPath {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    # unity-bridge.ps1과 동일 규약: ProjectVersion.txt → Hub 설치 경로.
    $versionFile = Join-Path $ProjectRoot 'ProjectSettings/ProjectVersion.txt'
    $content = Get-Content $versionFile -Raw
    if ($content -notmatch 'm_EditorVersion:\s*(\S+)') {
        throw "ProjectVersion.txt에서 에디터 버전을 읽지 못했다: $versionFile"
    }
    $version = $Matches[1]
    $hubPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (-not (Test-Path $hubPath)) {
        throw "Unity $version 이 Hub에 설치되어 있지 않다: $hubPath"
    }
    return $hubPath
}

function Write-FailureTail {
    param([string]$LogPath)
    if (-not (Test-Path $LogPath)) {
        Write-Host "  (로그 파일 없음: $LogPath)"
        return
    }
    $markers = Select-String -Path $LogPath -Pattern 'error CS|Exception|Scripts have compiler errors|Assertion failed|##### Error' |
        Select-Object -Last 12
    if ($markers) {
        Write-Host '  -- 로그 에러 마커 (마지막 12) --'
        $markers | ForEach-Object { Write-Host "  $($_.LineNumber): $($_.Line.Trim())" }
    }
    Write-Host '  -- 로그 tail (마지막 15줄) --'
    Get-Content $LogPath -Tail 15 | ForEach-Object { Write-Host "  $_" }
}

function Invoke-UnityPhase {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$UnityArgs,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][int]$TimeoutMinutes
    )
    Write-Host ''
    Write-Host "== phase: $Name (timeout ${TimeoutMinutes}m) =="
    Write-Host "   log: $LogPath"
    $started = Get-Date
    $process = Start-Process -FilePath $script:unityExe -ArgumentList $UnityArgs -PassThru -NoNewWindow
    if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Write-Host "phase '$Name' 타임아웃(${TimeoutMinutes}m) — 프로세스 강제 종료." -ForegroundColor Red
        Write-FailureTail -LogPath $LogPath
        return [pscustomobject]@{ Name = $Name; Ok = $false; ExitCode = -1; Reason = 'timeout'; Minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1) }
    }
    $minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    $exitCode = $process.ExitCode
    if ($exitCode -ne 0) {
        Write-Host "phase '$Name' 실패 (exit $exitCode, ${minutes}m)" -ForegroundColor Red
        Write-FailureTail -LogPath $LogPath
        return [pscustomobject]@{ Name = $Name; Ok = $false; ExitCode = $exitCode; Reason = "exit $exitCode"; Minutes = $minutes }
    }
    Write-Host "phase '$Name' 통과 (${minutes}m)"
    return [pscustomobject]@{ Name = $Name; Ok = $true; ExitCode = 0; Reason = 'ok'; Minutes = $minutes }
}

function Get-TestRunSummary {
    param([Parameter(Mandatory = $true)][string]$ResultsPath)
    if (-not (Test-Path $ResultsPath)) {
        return $null
    }
    [xml]$xml = Get-Content $ResultsPath -Raw
    $run = $xml.'test-run'
    $failedNames = @($xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { $_.fullname })
    return [pscustomobject]@{
        Total  = [int]$run.total
        Passed = [int]$run.passed
        Failed = [int]$run.failed
        Skipped = [int]$run.skipped
        FailedNames = $failedNames
    }
}

function Invoke-UnityTestPhase {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$TestPlatform,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][string]$ResultsPath,
        [Parameter(Mandatory = $true)][int]$TimeoutMinutes
    )
    # CRITICAL: -quit는 -runTests와 결합 금지(unity-bridge.ps1과 동일 규약).
    $testArgs = @(
        '-batchmode', '-nographics',
        '-projectPath', $script:CloneDir,
        '-runTests',
        '-testPlatform', $TestPlatform,
        '-testResults', $ResultsPath,
        '-logFile', $LogPath
    )
    $result = Invoke-UnityPhase -Name $Name -UnityArgs $testArgs -LogPath $LogPath -TimeoutMinutes $TimeoutMinutes
    $summary = Get-TestRunSummary -ResultsPath $ResultsPath
    if ($null -eq $summary) {
        if ($result.Ok) {
            $result.Ok = $false
            $result.Reason = 'no results file'
            Write-Host "phase '$Name': 결과 파일이 생성되지 않았다 — 실패 처리." -ForegroundColor Red
        }
        return $result
    }
    Write-Host ("   tests: total {0} / passed {1} / failed {2} / skipped {3}" -f $summary.Total, $summary.Passed, $summary.Failed, $summary.Skipped)
    if ($summary.Failed -gt 0) {
        $result.Ok = $false
        $result.Reason = "$($summary.Failed) failed"
        $summary.FailedNames | Select-Object -First 20 | ForEach-Object { Write-Host "   FAIL: $_" -ForegroundColor Red }
    }
    $result | Add-Member -NotePropertyName Tests -NotePropertyValue $summary
    return $result
}

# ---- preflight ----------------------------------------------------------
$cloneVolume = [System.IO.Path]::GetPathRoot((Resolve-Path (Split-Path $CloneDir -Parent)).Path)
$freeGb = [math]::Round((Get-PSDrive -Name $cloneVolume.TrimEnd(':', '\')).Free / 1GB, 1)
if ($freeGb -lt $MinFreeDiskGb) {
    throw "디스크 여유 공간 부족: $cloneVolume ${freeGb}GB < ${MinFreeDiskGb}GB"
}

$refSha = (git -C $RepoRoot rev-parse --short=8 $Ref 2>$null)
if ([string]::IsNullOrWhiteSpace($refSha)) {
    throw "ref를 해석하지 못했다: $Ref"
}

Write-Host "== clean-clone witness =="
Write-Host "   source: $RepoRoot"
Write-Host "   ref:    $Ref ($refSha)"
Write-Host "   clone:  $CloneDir"
Write-Host "   disk:   $cloneVolume ${freeGb}GB free"

# ---- clone --------------------------------------------------------------
if (Test-Path $CloneDir) {
    Write-Host '기존 witness 클론 제거 중...'
    Remove-Item -LiteralPath $CloneDir -Recurse -Force
}

Write-Host '클론 생성 중 (로컬 객체 공유, LFS smudge 포함)...'
git clone --branch $Ref --single-branch -- $RepoRoot $CloneDir
if ($LASTEXITCODE -ne 0) {
    # LFS smudge가 로컬 전송에서 실패하는 환경 폴백: pointer로 받고 명시 pull.
    Write-Host 'plain clone 실패 — GIT_LFS_SKIP_SMUDGE=1 폴백 시도.' -ForegroundColor Yellow
    if (Test-Path $CloneDir) { Remove-Item -LiteralPath $CloneDir -Recurse -Force }
    $env:GIT_LFS_SKIP_SMUDGE = '1'
    try {
        git clone --branch $Ref --single-branch -- $RepoRoot $CloneDir
        if ($LASTEXITCODE -ne 0) { throw "클론 실패 (exit $LASTEXITCODE)" }
        git -C $CloneDir lfs pull
        if ($LASTEXITCODE -ne 0) { throw "git lfs pull 실패 (exit $LASTEXITCODE)" }
    }
    finally {
        Remove-Item Env:GIT_LFS_SKIP_SMUDGE -ErrorAction SilentlyContinue
    }
}

$cloneSha = (git -C $CloneDir rev-parse HEAD).Trim()
$lfsCount = @(git -C $CloneDir lfs ls-files).Count
Write-Host "클론 완료: HEAD $($cloneSha.Substring(0,8)), LFS 파일 $lfsCount 개"

# ---- machine-only 에셋 복사 (신규 머신 "에셋팩 임포트" 절차의 등가) --------
$copiedAssetDirs = @()
$missingAssetDirs = @()
if (-not $NoMachineOnlyAssetCopy) {
    foreach ($rel in $script:MachineOnlyAssetDirs) {
        $src = Join-Path $RepoRoot $rel
        if (-not (Test-Path $src)) {
            $missingAssetDirs += $rel
            continue
        }
        $dst = Join-Path $CloneDir $rel
        # robocopy: 0-7 = 성공 계열 exit code
        robocopy $src $dst /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) {
            throw "machine-only 에셋 복사 실패($rel): robocopy exit $LASTEXITCODE"
        }
        $srcMeta = "$src.meta"
        if (Test-Path $srcMeta) {
            Copy-Item -LiteralPath $srcMeta -Destination "$dst.meta" -Force
        }
        $copiedAssetDirs += $rel
    }
    Write-Host "machine-only 에셋 복사: $($copiedAssetDirs.Count)/$($script:MachineOnlyAssetDirs.Count) 디렉터리"
    if ($missingAssetDirs.Count -gt 0) {
        Write-Host "  원본에 없는 디렉터리(스킵): $($missingAssetDirs -join ', ')" -ForegroundColor Yellow
    }
}
else {
    Write-Host 'strict 모드: machine-only 에셋 미복사 — 유료팩/승격아트 의존 테스트 실패가 예상된다.' -ForegroundColor Yellow
}

$logsDir = Join-Path $CloneDir 'Logs/witness'
New-Item -ItemType Directory -Force $logsDir | Out-Null
$script:CloneDir = $CloneDir
$script:unityExe = Resolve-UnityEditorPath -ProjectRoot $CloneDir
Write-Host "Unity: $script:unityExe"

# ---- phases -------------------------------------------------------------
$phases = @()

# phase 1: 전체 import + 컴파일 + 콘텐츠 검증 (첫 import라 가장 무겁다)
$phases += Invoke-UnityPhase -Name 'content-validate (import+compile 포함)' -TimeoutMinutes $ImportPhaseTimeoutMinutes `
    -LogPath (Join-Path $logsDir 'content-validation.log') `
    -UnityArgs @(
        '-batchmode', '-nographics',
        '-projectPath', $CloneDir,
        '-quit',
        '-executeMethod', 'SM.Editor.Validation.ValidationBatchEntryPoint.RunContentValidation',
        '-logFile', (Join-Path $logsDir 'content-validation.log')
    )

if ($phases[-1].Ok -and -not $SkipBalanceSweep) {
    $phases += Invoke-UnityPhase -Name 'balance-sweep-smoke' -TimeoutMinutes $PhaseTimeoutMinutes `
        -LogPath (Join-Path $logsDir 'balance-sweep.log') `
        -UnityArgs @(
            '-batchmode', '-nographics',
            '-projectPath', $CloneDir,
            '-quit',
            '-executeMethod', 'SM.Editor.Validation.ValidationBatchEntryPoint.RunBalanceSweepSmoke',
            '-logFile', (Join-Path $logsDir 'balance-sweep.log')
        )
}

if ($phases[0].Ok) {
    $phases += Invoke-UnityTestPhase -Name 'editmode-tests' -TestPlatform 'EditMode' -TimeoutMinutes $PhaseTimeoutMinutes `
        -LogPath (Join-Path $logsDir 'editmode-tests.log') `
        -ResultsPath (Join-Path $logsDir 'editmode-results.xml')

    if ($IncludePlayMode) {
        $phases += Invoke-UnityTestPhase -Name 'playmode-tests' -TestPlatform 'PlayMode' -TimeoutMinutes $PhaseTimeoutMinutes `
            -LogPath (Join-Path $logsDir 'playmode-tests.log') `
            -ResultsPath (Join-Path $logsDir 'playmode-results.xml')
    }
}
else {
    Write-Host 'import/컴파일 phase 실패 — 후속 phase 생략.' -ForegroundColor Red
}

# ---- summary ------------------------------------------------------------
$allOk = -not ($phases | Where-Object { -not $_.Ok })
$summary = [pscustomobject]@{
    Ref = $Ref
    Sha = $cloneSha
    CloneDir = $CloneDir
    RanAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    AllOk = $allOk
    MachineOnlyAssetsCopied = $copiedAssetDirs
    MachineOnlyAssetsMissing = $missingAssetDirs
    Phases = $phases
}
$summaryPath = Join-Path $logsDir 'witness-summary.json'
$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $summaryPath -Encoding utf8

Write-Host ''
Write-Host '== witness 결과 =='
$phases | ForEach-Object {
    $mark = if ($_.Ok) { 'PASS' } else { 'FAIL' }
    Write-Host ("  [{0}] {1} — {2} ({3}m)" -f $mark, $_.Name, $_.Reason, $_.Minutes)
}
Write-Host "  summary: $summaryPath"

if ($allOk) {
    if ($KeepClone) {
        Write-Host "클론 보존(-KeepClone): $CloneDir"
    }
    else {
        # 요약/로그만 보존하고 클론 제거
        $keepDir = Join-Path ([System.IO.Path]::GetTempPath()) "sm-witness-logs-$($cloneSha.Substring(0,8))"
        New-Item -ItemType Directory -Force $keepDir | Out-Null
        Copy-Item -Path (Join-Path $logsDir '*') -Destination $keepDir -Recurse -Force
        Write-Host "클론 정리 중... (로그 사본: $keepDir)"
        Remove-Item -LiteralPath $CloneDir -Recurse -Force
    }
    Write-Host "clean-clone witness PASS — $Ref ($($cloneSha.Substring(0,8)))" -ForegroundColor Green
    exit 0
}

Write-Host "clean-clone witness FAIL — 클론과 로그를 보존한다: $CloneDir" -ForegroundColor Red
exit 1

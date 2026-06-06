<#
.SYNOPSIS
cross-platform canonical-hash 코퍼스 produce/compare 러너 (ADR-0029 / Phase 0 cross-platform 오라클).

.DESCRIPTION
CI/디바이스가 각 backend(Editor Mono / x86_64 IL2CPP / ARM64 IL2CPP)에서 `-Produce`로 결정적 hash
코퍼스를 덤프하고, `-Compare`로 두 backend 코퍼스가 동일한지 단언한다. 다르면 첫 divergent seed/step을
찍어 desync 위치를 좁힌다(tick-level first-divergence).

현 sim은 float이라 same-binary만 일치한다(cross-platform bit-exact는 Fixed 마이그레이션 후). Phase 0는
harness 스켈레톤 + 관측이다 — 실제 backend matrix 실행은 CI/디바이스 팜에서 수행하고, 골든은 마이그레이션
이후 Phase 6에서 hard gate로 commit한다.

.PARAMETER Produce
batch `-executeMethod`로 현 backend 코퍼스를 TestResults/hash-corpus-<Backend>.txt 로 덤프
(tools/unity-execute-method.ps1 재사용). 주의: 열린 GUI 에디터와 프로젝트 락이 충돌하므로 에디터를 닫고 실행.

.PARAMETER Golden
비교 기준 코퍼스 경로. `-Candidate`와 함께 주면 compare 모드(Unity 불필요). 동일하면 exit 0, 다르면
first-divergence 보고 후 exit 1.

.PARAMETER Candidate
비교 대상 코퍼스 경로.

.EXAMPLE
pwsh -File tools/hash-corpus-check.ps1 -Produce -Backend android-arm64-il2cpp
pwsh -File tools/hash-corpus-check.ps1 -Golden TestResults/hash-corpus-editor-mono.txt -Candidate TestResults/hash-corpus-android-arm64-il2cpp.txt
#>

param(
    [string]$RepoRoot = ".",
    [switch]$Produce,
    [string]$Backend = "editor-mono",
    [string]$Golden,
    [string]$Candidate
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

function Invoke-Produce {
    param([string]$BackendTag)

    $running = Get-Process Unity -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero }
    if ($running) {
        Write-Warning "GUI Unity 에디터가 실행 중입니다(pid=$($running.Id -join ',')). batchmode는 프로젝트 락이 충돌합니다 — 에디터를 닫고 다시 실행하세요."
        return 2
    }

    $defaultOut = Join-Path 'TestResults' 'hash-corpus.txt'
    $taggedOut = Join-Path 'TestResults' "hash-corpus-$BackendTag.txt"

    # unity-execute-method.ps1 재사용 — 엔트리는 기본 경로(TestResults/hash-corpus.txt)에 쓴다.
    pwsh -File (Join-Path $PSScriptRoot 'unity-execute-method.ps1') `
        -Method 'SM.Editor.Determinism.BattleHashCorpusDumpEntry.Run' `
        -LogFile (Join-Path 'TestResults' "hash-corpus-$BackendTag.log") `
        -PhaseName "hash-corpus:$BackendTag"

    if (-not (Test-Path $defaultOut)) {
        Write-Error "코퍼스 파일이 생성되지 않았습니다: $defaultOut"
        return 1
    }

    Move-Item -LiteralPath $defaultOut -Destination $taggedOut -Force
    Write-Host "corpus produced: $taggedOut"
    return 0
}

function Invoke-Compare {
    param([string]$GoldenPath, [string]$CandidatePath)

    foreach ($p in @($GoldenPath, $CandidatePath)) {
        if (-not (Test-Path $p)) {
            Write-Error "코퍼스 파일 없음: $p"
            return 1
        }
    }

    $golden = Get-Content -LiteralPath $GoldenPath
    $candidate = Get-Content -LiteralPath $CandidatePath
    $max = [Math]::Max($golden.Count, $candidate.Count)
    $seedContext = '(pre-seed)'

    for ($i = 0; $i -lt $max; $i++) {
        $g = if ($i -lt $golden.Count) { $golden[$i] } else { $null }
        $c = if ($i -lt $candidate.Count) { $candidate[$i] } else { $null }
        if ($g -ne $null -and $g.StartsWith('seed=')) { $seedContext = $g }

        if ($g -ne $c) {
            Write-Host '== hash-corpus DIVERGENCE =='
            Write-Host "  context : $seedContext"
            Write-Host "  line    : $($i + 1)"
            Write-Host "  golden  : $g"
            Write-Host "  candidate: $c"
            Write-Error "코퍼스 불일치 — backend 간 sim 발산(first-divergence는 위 seed/step)."
            return 1
        }
    }

    Write-Host "hash-corpus MATCH ($($golden.Count) lines): $GoldenPath == $CandidatePath"
    return 0
}

if ($Produce) {
    exit (Invoke-Produce -BackendTag $Backend)
}

if ($Golden -and $Candidate) {
    exit (Invoke-Compare -GoldenPath $Golden -CandidatePath $Candidate)
}

Write-Host 'usage:'
Write-Host '  -Produce -Backend <tag>             # batch 덤프 → TestResults/hash-corpus-<tag>.txt (에디터 닫고)'
Write-Host '  -Golden <path> -Candidate <path>    # 두 코퍼스 비교, first-divergence 보고'
exit 0

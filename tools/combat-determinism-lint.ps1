<#
.SYNOPSIS
SM.Combat authoritative sim의 고정소수점 결정론 경계 위반을 스캔한다.

.DESCRIPTION
ADR-0029 / docs/03_architecture/deterministic-sim-and-fixed-point-migration.md Phase 0 가드.
authoritative sim 표면(SM.Combat)에서 cross-platform 비결정 토큰(float/double/decimal/MathF/
System.Random 등)을 스캔한다.

- 기본은 INVENTORY 모드: 현재 float 표면을 파일별로 보고하고 BASELINE 수치를 찍은 뒤 exit 0.
  마이그레이션이 진행되며 이 수치가 줄어드는 것을 추적한다("관측 가능성 우선, behavior 변화 0").
- -Strict: 비-allowlist 위반이 하나라도 있으면 exit 1. 마이그레이션 완료(Phase 5+) 후 hard gate로 승격.

egress(read-model projection) / telemetry / display 표면은 float 허용(경계 밖) — 파일명 allowlist.
allowlist는 provisional이며 후속 단계에서 좁힌다.

주의: Vector2/Mathf/UnityEngine은 SM.Combat asmdef가 UnityEngine을 참조하지 않아 이미 차단되지만,
방어적으로 함께 스캔한다. 주석-only 라인은 휴리스틱으로 제외한다(블록 주석은 후속 개선).
#>

param(
    [string]$RepoRoot = ".",
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

$simRoot = 'Assets/_Game/Scripts/Runtime/Combat'
if (-not (Test-Path $simRoot)) {
    Write-Error "Authoritative sim 경로를 찾을 수 없습니다: $simRoot"
    exit 1
}

# float이 허용되는 egress/telemetry/display 표면 (경계 밖). 파일명 기준, provisional.
$allowList = @(
    'BattleReadModelBuilder.cs',          # egress: Fixed -> float read-model projection
    'BattleSimulationStep.cs',            # read-model record (display float fields)
    'BattleActivityTelemetry.cs',         # telemetry/analysis (display)
    'BattleTelemetryAnalysisService.cs',  # analysis (display)
    'BattleTelemetryRecorder.cs',         # telemetry recording (observation)
    'LoopDTelemetryModels.cs',            # telemetry models (display)
    'ClusterTradeoffModels.cs'            # telemetry models (display)
)

# 비결정 토큰 (C# 키워드는 case-sensitive: FloatRange 같은 타입명 오탐 방지).
$patterns = @(
    @{ Name = 'float';         Regex = '\bfloat\b';                          Cased = $true  },
    @{ Name = 'double';        Regex = '\bdouble\b';                         Cased = $true  },
    @{ Name = 'decimal';       Regex = '\bdecimal\b';                        Cased = $true  },
    @{ Name = 'MathF';         Regex = '\bMathF\.';                          Cased = $true  },
    @{ Name = 'Mathf(Unity)';  Regex = '\bMathf\.';                          Cased = $true  },
    @{ Name = 'Vector2/3';     Regex = '\bVector[23]\b';                     Cased = $true  },
    @{ Name = 'UnityEngine';   Regex = '\bUnityEngine\b';                    Cased = $true  },
    @{ Name = 'System.Random'; Regex = '(System\.Random|\bnew\s+Random\s*\()'; Cased = $false }
)

function Test-CommentOnly([string]$line) {
    $t = $line.TrimStart()
    return ($t.StartsWith('//') -or $t.StartsWith('*') -or $t.StartsWith('/*'))
}

$files = Get-ChildItem -Path $simRoot -Recurse -Filter *.cs -File
$perFile = [System.Collections.Generic.List[object]]::new()
$tokenTally = [ordered]@{}
foreach ($p in $patterns) { $tokenTally[$p.Name] = 0 }

$totalHits = 0
$allowHits = 0

foreach ($file in $files) {
    $name = $file.Name
    $isAllowed = $allowList -contains $name
    $lines = Get-Content -LiteralPath $file.FullName
    $fileHits = 0
    $lineNo = 0
    foreach ($line in $lines) {
        $lineNo++
        if (Test-CommentOnly $line) { continue }
        foreach ($p in $patterns) {
            $matched = if ($p.Cased) { $line -cmatch $p.Regex } else { $line -match $p.Regex }
            if ($matched) {
                $fileHits++
                if (-not $isAllowed) { $tokenTally[$p.Name]++ }
            }
        }
    }

    if ($fileHits -gt 0) {
        $perFile.Add([pscustomobject]@{ File = $name; Hits = $fileHits; Allowed = $isAllowed })
        if ($isAllowed) { $allowHits += $fileHits } else { $totalHits += $fileHits }
    }
}

Write-Host '== combat-determinism-lint (Phase 0 inventory) =='
Write-Host "scope    : $simRoot (authoritative sim 표면)"
Write-Host "scanned  : $($files.Count) files"
Write-Host "allowlist: $($allowList.Count) files (egress/telemetry — float 허용)"
Write-Host ''
Write-Host 'float-surface hits by file (비-allowlist, hit 내림차순):'
foreach ($row in ($perFile | Where-Object { -not $_.Allowed } | Sort-Object Hits -Descending)) {
    Write-Host ("  {0,-44} {1,5}" -f $row.File, $row.Hits)
}

$allowed = $perFile | Where-Object { $_.Allowed }
if ($allowed) {
    Write-Host ''
    Write-Host 'allowlist (float-OK, 정보용):'
    foreach ($row in ($allowed | Sort-Object Hits -Descending)) {
        Write-Host ("  {0,-44} {1,5}" -f $row.File, $row.Hits)
    }
}

Write-Host ''
Write-Host 'per-token tally (비-allowlist):'
foreach ($k in $tokenTally.Keys) {
    Write-Host ("  {0,-16} {1,5}" -f $k, $tokenTally[$k])
}

$nonAllowFiles = ($perFile | Where-Object { -not $_.Allowed }).Count
Write-Host ''
Write-Host ("BASELINE: {0} hits across {1} authoritative files (+{2} hits in {3} allowlisted)." -f `
    $totalHits, $nonAllowFiles, $allowHits, ($allowed | Measure-Object).Count)

if ($Strict) {
    if ($totalHits -gt 0) {
        Write-Error "Strict gate FAIL: authoritative SM.Combat에 비결정 토큰 ${totalHits}건 (마이그레이션 미완)."
        exit 1
    }
    Write-Host 'Strict gate PASS: authoritative 표면에 float/MathF/System.Random 없음.'
}

exit 0

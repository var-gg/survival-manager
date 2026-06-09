<#
.SYNOPSIS
USS 디자인 토큰 conformance lint — ThemeTokens(SoT) 정합성을 강제한다.

.DESCRIPTION
ThemeTokens.uss가 정의는 됐으나 강제되지 않아 surface마다 px/색 리터럴이 흩어지고,
미정의 토큰을 참조해 배경/칩이 조용히 빠지는 렌더 버그(2026-06 batch 8에서 4건)가 누적됐다.
본 lint는 그 두 클래스를 자동으로 잡는다.

검사 항목:
  1. [ERROR] 미정의 var() 참조 (fallback 없음)  — 어디에도 정의되지 않은 토큰 참조 = 렌더 버그
  2. [WARN ] 미정의 var() 참조 (fallback 있음)  — 토큰 누락이나 fallback으로 degrade
  3. [METRIC] font-size px 리터럴               — --sm-fs-* 미채택 정량
  4. [METRIC] raw color 리터럴(rgb/rgba/#hex)   — 토큰 미채택 정량 (ThemeTokens 제외)

종료 코드: ERROR(미정의 no-fallback)가 하나라도 있으면 1, 아니면 0.
METRIC/WARN은 정보용 — 기본적으로 빌드를 깨지 않는다(-StrictMetrics로 임계 강제 가능).

.PARAMETER RepoRoot
저장소 루트. 기본 '.'.

.PARAMETER Json
사람용 표 대신 JSON 요약을 출력.

.PARAMETER StrictMetrics
지정 시 METRIC 합계가 baseline(아래 상수)을 초과하면 종료 코드 2. 회귀 방지용.
#>

param(
    [string]$RepoRoot = ".",
    [switch]$Json,
    [switch]$StrictMetrics
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path $RepoRoot).Path

# ThemeTokens(SoT) 위치
$themeTokensRel = 'Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss'

# USS 수집 — _Game 하위 전부
$ussRoot = Join-Path $repo 'Assets/_Game'
$ussFiles = Get-ChildItem -Path $ussRoot -Recurse -Filter '*.uss' -File |
    Sort-Object FullName

if ($ussFiles.Count -eq 0) {
    Write-Error "USS 파일을 찾지 못했습니다: $ussRoot"
    exit 1
}

# 주석 제거: /* ... */ (멀티라인). 라인번호 보존을 위해 주석을 같은 줄 수의 공백/개행으로 치환.
function Remove-UssComments([string]$text) {
    return [System.Text.RegularExpressions.Regex]::Replace(
        $text,
        '/\*.*?\*/',
        { param($m) ($m.Value -replace '[^\r\n]', ' ') },
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
}

# 1차 패스: 모든 USS에서 토큰 '정의' 수집 (--name: ...)
$definedTokens = [System.Collections.Generic.HashSet[string]]::new()
$defRegex = [regex]'(?m)(?:^|[;{])\s*(--[A-Za-z0-9-]+)\s*:'
$fileTexts = @{}

foreach ($f in $ussFiles) {
    $raw = Get-Content -LiteralPath $f.FullName -Raw
    $clean = Remove-UssComments $raw
    $fileTexts[$f.FullName] = $clean
    foreach ($m in $defRegex.Matches($clean)) {
        [void]$definedTokens.Add($m.Groups[1].Value)
    }
}

# 2차 패스: 참조/리터럴 분석
$refRegex     = [regex]'var\(\s*(--[A-Za-z0-9-]+)\s*(,[^()]*)?\)'
$fontLitRegex = [regex]'(?m)font-size\s*:\s*(\d+(?:\.\d+)?)px'
$colorRegex   = [regex]'(?i)(?:rgba?\(|#[0-9a-f]{3,8}\b)'

$errors  = New-Object System.Collections.Generic.List[object]   # 미정의 no-fallback
$warns   = New-Object System.Collections.Generic.List[object]   # 미정의 w/ fallback
$metrics = New-Object System.Collections.Generic.List[object]   # per-file METRIC

$totalFontLits = 0
$totalRawColors = 0

foreach ($f in $ussFiles) {
    $rel = $f.FullName.Substring($repo.Length).TrimStart('\','/') -replace '\\','/'
    $clean = $fileTexts[$f.FullName]
    $lines = $clean -split "`n"

    # 미정의 var() 참조
    foreach ($m in $refRegex.Matches($clean)) {
        $name = $m.Groups[1].Value
        if ($definedTokens.Contains($name)) { continue }
        $hasFallback = $m.Groups[2].Success
        # 라인번호
        $idx = $m.Index
        $lineNo = ($clean.Substring(0, $idx) -split "`n").Count
        $rec = [pscustomobject]@{ file = $rel; line = $lineNo; token = $name; fallback = $hasFallback }
        if ($hasFallback) { $warns.Add($rec) } else { $errors.Add($rec) }
    }

    # font-size px 리터럴
    $fontHits = @()
    foreach ($m in $fontLitRegex.Matches($clean)) {
        $idx = $m.Index
        $lineNo = ($clean.Substring(0, $idx) -split "`n").Count
        $fontHits += [pscustomobject]@{ line = $lineNo; px = $m.Groups[1].Value }
    }

    # raw color 리터럴 (ThemeTokens는 SoT라 제외)
    $rawColors = 0
    if ($rel -ne $themeTokensRel) {
        $rawColors = $colorRegex.Matches($clean).Count
    }

    $totalFontLits  += $fontHits.Count
    $totalRawColors += $rawColors

    if ($fontHits.Count -gt 0 -or $rawColors -gt 0) {
        $metrics.Add([pscustomobject]@{
            file       = $rel
            fontLits   = $fontHits.Count
            fontLines  = ($fontHits | ForEach-Object { "$($_.line):$($_.px)px" }) -join ', '
            rawColors  = $rawColors
        })
    }
}

# baseline (회귀 방지 — 현재 측정치를 천장으로). 토큰 채택이 진행되면 이 값을 내려간다.
# 2026-06 batch 9-11 render-identical 배선 후 측정치로 lock. 이보다 늘면 StrictMetrics 실패.
$BASELINE_FONT_LITS  = 222
$BASELINE_RAW_COLORS = 1939

if ($Json) {
    $out = [pscustomobject]@{
        definedTokens   = $definedTokens.Count
        ussFiles        = $ussFiles.Count
        errors          = $errors
        warns           = $warns
        totalFontLits   = $totalFontLits
        totalRawColors  = $totalRawColors
        metrics         = $metrics
    }
    $out | ConvertTo-Json -Depth 6
} else {
    Write-Host ""
    Write-Host "=== USS 토큰 conformance lint ===" -ForegroundColor Cyan
    Write-Host ("정의된 토큰: {0}    스캔 USS: {1}" -f $definedTokens.Count, $ussFiles.Count)
    Write-Host ""

    if ($errors.Count -gt 0) {
        Write-Host ("[ERROR] 미정의 토큰 참조 (fallback 없음 — 렌더 버그) : {0}건" -f $errors.Count) -ForegroundColor Red
        foreach ($e in $errors) {
            Write-Host ("  {0}:{1}  {2}" -f $e.file, $e.line, $e.token) -ForegroundColor Red
        }
        Write-Host ""
    } else {
        Write-Host "[OK] 미정의 토큰 참조(no-fallback) 없음 — 렌더버그 0" -ForegroundColor Green
    }

    if ($warns.Count -gt 0) {
        Write-Host ("[WARN] 미정의 토큰 참조 (fallback 있음) : {0}건" -f $warns.Count) -ForegroundColor Yellow
        foreach ($w in $warns) {
            Write-Host ("  {0}:{1}  {2} (fallback)" -f $w.file, $w.line, $w.token) -ForegroundColor Yellow
        }
        Write-Host ""
    }

    Write-Host ("[METRIC] font-size px 리터럴 합계 : {0}  (→ --sm-fs-* 미채택)" -f $totalFontLits) -ForegroundColor DarkYellow
    Write-Host ("[METRIC] raw color 리터럴 합계    : {0}  (→ 토큰 미채택, ThemeTokens 제외)" -f $totalRawColors) -ForegroundColor DarkYellow
    Write-Host ""
    if ($metrics.Count -gt 0) {
        Write-Host "파일별 드리프트 (상위 font 리터럴 기준):" -ForegroundColor DarkGray
        foreach ($mrec in ($metrics | Sort-Object -Property fontLits -Descending | Select-Object -First 20)) {
            Write-Host ("  {0,3} font / {1,3} color  {2}" -f $mrec.fontLits, $mrec.rawColors, $mrec.file) -ForegroundColor DarkGray
        }
    }
    Write-Host ""
}

# 종료 코드
if ($errors.Count -gt 0) {
    if (-not $Json) { Write-Host ("FAIL — 미정의 토큰 참조 {0}건. ThemeTokens에 정의하거나 참조를 고치세요." -f $errors.Count) -ForegroundColor Red }
    exit 1
}

if ($StrictMetrics -and ($totalFontLits -gt $BASELINE_FONT_LITS -or $totalRawColors -gt $BASELINE_RAW_COLORS)) {
    if (-not $Json) { Write-Host "FAIL(StrictMetrics) — 드리프트 baseline 초과" -ForegroundColor Red }
    exit 2
}

if (-not $Json) { Write-Host "PASS — 미정의 토큰 참조 없음." -ForegroundColor Green }
exit 0

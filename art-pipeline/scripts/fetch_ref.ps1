<#
.SYNOPSIS
    pindoc asset blob을 LocalFS storage에서 docker cp로 직접 가져온다.
.DESCRIPTION
    pindoc localfs storage layout: {ASSET_ROOT}/{sha256[0:2]}/{sha256} (no ext).
    asset_read MCP로 sha256을 먼저 받은 뒤 이 helper에 넘긴다.
    blob URL 다운로드보다 한 단계 짧고, 같은 머신이라 안정적.
.PARAMETER Sha256
    Asset sha256 (소문자 hex 64자). pindoc.asset.read MCP의 응답에서 추출.
.PARAMETER Out
    Host 측 저장 경로. 부모 디렉토리 자동 생성.
.PARAMETER Container
    pindoc 서버 컨테이너 이름. 기본: pindoc-server-daemon.
.EXAMPLE
    pwsh -File art-pipeline/scripts/fetch_ref.ps1 `
        -Sha256 0de5ef6c66c95ea882dcbe4b423fde9109feb3ddd625d3d8c01df09c56763793 `
        -Out art-pipeline/ref/characters/hero_dawn_priest/p09_idle_rev9.png
#>
param(
    [Parameter(Mandatory)][string]$Sha256,
    [Parameter(Mandatory)][string]$Out,
    [string]$Container = "pindoc-server-daemon"
)

if ($Sha256 -notmatch '^[0-9a-f]{64}$') {
    Write-Error "Sha256 must be 64-char lowercase hex. Got: $Sha256"
    exit 2
}

$prefix = $Sha256.Substring(0, 2)
$src = "${Container}:/var/lib/pindoc/assets/${prefix}/${Sha256}"

$outDir = Split-Path $Out -Parent
if ($outDir) { New-Item -Path $outDir -ItemType Directory -Force | Out-Null }

if (Test-Path $Out) {
    $existing = (Get-FileHash $Out -Algorithm SHA256).Hash.ToLower()
    if ($existing -eq $Sha256) {
        Write-Output "SKIP: $Out (already matches sha256)"
        exit 0
    }
    Write-Output "OVERWRITE: existing file at $Out has different sha"
}

docker cp $src $Out
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Out)) {
    Write-Error "docker cp failed for $src (exit=$LASTEXITCODE)"
    exit 1
}

$got = (Get-FileHash $Out -Algorithm SHA256).Hash.ToLower()
if ($got -ne $Sha256) {
    Write-Error "sha mismatch after copy: got=$got expected=$Sha256"
    Remove-Item $Out
    exit 3
}

$f = Get-Item $Out
Write-Output "OK: $($f.FullName) ($($f.Length) bytes, sha verified)"

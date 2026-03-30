param(
    [string]$RepoRoot = ".",
    [switch]$PolicyOnly
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

Write-Host '== docs-policy-check =='
pwsh -File tools/docs-policy-check.ps1 -RepoRoot .

if ($PolicyOnly) {
    exit 0
}

Write-Host '== markdownlint =='
npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host '== markdown-link-check =='
$markdownTargets = @()
$markdownTargets += Get-ChildItem . -File -Filter *.md | Select-Object -ExpandProperty FullName
$markdownTargets += Get-ChildItem docs -Recurse -File -Filter *.md | Select-Object -ExpandProperty FullName
$markdownTargets = $markdownTargets | Sort-Object -Unique

$linkCheckFailed = $false
foreach ($target in $markdownTargets) {
    npx --yes markdown-link-check $target --quiet
    if ($LASTEXITCODE -ne 0) {
        $linkCheckFailed = $true
    }
}

if ($linkCheckFailed) {
    exit 1
}

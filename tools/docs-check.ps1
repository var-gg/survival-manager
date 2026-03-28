param(
    [string]$RepoRoot = "."
)

$ErrorActionPreference = 'Stop'
Set-Location $RepoRoot

Write-Host '== markdownlint =='
npx --yes markdownlint-cli2 "**/*.md" "#Library/**" "#Logs/**" "#.git/**"

Write-Host '== markdown-link-check =='
npx --yes markdown-link-check "docs/**/*.md" "*.md" --quiet

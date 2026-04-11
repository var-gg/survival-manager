#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Narrative seed manifest builder wrapper.
.DESCRIPTION
    Runs the Python narrative parser to generate Temp/Narrative/narrative-seed.json.
#>
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

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
exit $LASTEXITCODE

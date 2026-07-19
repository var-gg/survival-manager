<#
.SYNOPSIS
Runs the campaign headless-sweep cross-runtime determinism gate under .NET 8.

.DESCRIPTION
Builds the real SM.Core, SM.Combat, and SM.Meta source through tools/HeadlessSweep,
runs BattleHashCorpus.Generate(), and compares its CRLF-normalized output with the
checked-in Unity golden. A local .NET 8 SDK is preferred; when none is available,
the official mcr.microsoft.com/dotnet/sdk:8.0 Docker image is used.

.EXAMPLE
pwsh -File tools/headless-sweep-check.ps1
#>

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$taskRepoRoot = Split-Path -Parent $PSScriptRoot
$taskProjectPath = Join-Path $taskRepoRoot 'tools/HeadlessSweep/HeadlessSweep.csproj'
$taskDotnet = Get-Command dotnet -ErrorAction SilentlyContinue

if ($taskDotnet) {
    $taskSdkList = & $taskDotnet.Source --list-sdks
    if ($taskSdkList | Where-Object { $_ -match '^8\.' }) {
        & $taskDotnet.Source run --project $taskProjectPath --configuration $Configuration
        exit $LASTEXITCODE
    }
}

$taskDocker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $taskDocker) {
    Write-Error 'A .NET 8 SDK or Docker is required to run the headless-sweep gate.'
    exit 2
}

$taskDockerArguments = @(
    'run'
    '--rm'
    '--volume'
    "${taskRepoRoot}:/workspace"
    '--workdir'
    '/workspace'
    'mcr.microsoft.com/dotnet/sdk:8.0'
    'dotnet'
    'run'
    '--project'
    'tools/HeadlessSweep/HeadlessSweep.csproj'
    '--configuration'
    $Configuration
)

& $taskDocker.Source @taskDockerArguments
exit $LASTEXITCODE

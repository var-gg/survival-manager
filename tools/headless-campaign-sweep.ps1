<#
.SYNOPSIS
Runs the parallel .NET 8 naive/informed campaign sweep.

.EXAMPLE
pwsh -File tools/headless-campaign-sweep.ps1 -Cells 24 -Verify
#>

param(
    [ValidateRange(1, 480)]
    [int]$Cells = 24,

    [ValidateRange(1, 512)]
    [int]$Degree = [Environment]::ProcessorCount,

    [string]$StopAfter = 'site_wolfpine_trail_boss_1',

    [string]$Output = 'Temp/HeadlessSweep/headless-campaign-sweep.json',

    [switch]$Verify,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$taskRepoRoot = Split-Path -Parent $PSScriptRoot
$taskProjectPath = Join-Path $taskRepoRoot 'tools/HeadlessSweep/HeadlessSweep.csproj'
$taskArguments = @(
    'campaign-sweep'
    '--cells'
    $Cells.ToString([Globalization.CultureInfo]::InvariantCulture)
    '--degree'
    $Degree.ToString([Globalization.CultureInfo]::InvariantCulture)
    '--stop-after'
    $StopAfter
    '--output'
    $Output
)
if ($Verify) {
    $taskArguments += '--verify'
}

$taskDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($taskDotnet) {
    $taskSdkList = & $taskDotnet.Source --list-sdks
    if ($taskSdkList | Where-Object { $_ -match '^8\.' }) {
        & $taskDotnet.Source run --project $taskProjectPath --configuration $Configuration -- @taskArguments
        exit $LASTEXITCODE
    }
}

$taskDocker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $taskDocker) {
    Write-Error 'A .NET 8 SDK or Docker is required to run the headless campaign sweep.'
    exit 2
}

$taskDockerOutput = $Output.Replace('\', '/')
if ([IO.Path]::IsPathRooted($Output)) {
    $taskOutputFullPath = [IO.Path]::GetFullPath($Output)
    $taskRootFullPath = [IO.Path]::GetFullPath($taskRepoRoot).TrimEnd('\') + '\'
    if (-not $taskOutputFullPath.StartsWith($taskRootFullPath, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Error 'When Docker is used, -Output must be inside the repository workspace.'
        exit 2
    }

    $taskDockerOutput = $taskOutputFullPath.Substring($taskRootFullPath.Length).Replace('\', '/')
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
    '--'
)
$taskDockerArguments += $taskArguments
$outputIndex = [Array]::IndexOf($taskDockerArguments, '--output')
if ($outputIndex -ge 0) {
    $taskDockerArguments[$outputIndex + 1] = $taskDockerOutput
}

& $taskDocker.Source @taskDockerArguments
exit $LASTEXITCODE

<#
.SYNOPSIS
Measures endless Heat equipment distribution and fixed/paired endgame clear rates.

.EXAMPLE
pwsh -File tools/endless-heat-sweep.ps1 -Seeds 32 -Horizons 25,100 -PairedHorizon 25
#>

param(
    [ValidateRange(1, 512)]
    [int]$Seeds = 32,

    [ValidateRange(1, 512)]
    [int]$Degree = [Environment]::ProcessorCount,

    [ValidateNotNullOrEmpty()]
    [string]$Horizons = '25,100',

    [ValidateRange(1, 1000)]
    [int]$PairedHorizon = 25,

    [string]$Output = 'Temp/HeadlessSweep/endless-heat-sweep.json',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$taskRepoRoot = Split-Path -Parent $PSScriptRoot
$taskProjectPath = Join-Path $taskRepoRoot 'tools/HeadlessSweep/HeadlessSweep.csproj'
$taskArguments = @(
    'endless-heat-sweep'
    '--seeds'
    $Seeds.ToString([Globalization.CultureInfo]::InvariantCulture)
    '--degree'
    $Degree.ToString([Globalization.CultureInfo]::InvariantCulture)
    '--horizons'
    $Horizons
    '--paired-horizon'
    $PairedHorizon.ToString([Globalization.CultureInfo]::InvariantCulture)
    '--output'
    $Output
)

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
    Write-Error 'A .NET 8 SDK or Docker is required to run the endless Heat sweep.'
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

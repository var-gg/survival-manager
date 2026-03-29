param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('status', 'list', 'compile', 'clear-console', 'console', 'bootstrap', 'test-edit', 'test-play', 'report-town', 'report-battle', 'smoke-observer', 'exec')]
    [string]$Verb,
    [int]$Lines = 30,
    [string]$Filter = 'error,warning,log',
    [string]$Code,
    [switch]$Dangerous
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$projectRootForUnityCli = ((Resolve-Path $projectRoot).Path -replace '\\', '/')
Set-Location $projectRoot

function Resolve-UnityCliPath {
    $command = Get-Command unity-cli -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $fallback = Join-Path $env:LOCALAPPDATA 'unity-cli\unity-cli.exe'
    if (Test-Path $fallback) {
        return $fallback
    }

    throw "unity-cli executable not found. Install it first with: irm https://raw.githubusercontent.com/youngwoocho02/unity-cli/master/install.ps1 | iex"
}

function Invoke-UnityCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $unityCli = Resolve-UnityCliPath
    & $unityCli --project $projectRootForUnityCli @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "unity-cli command failed with exit code $LASTEXITCODE"
    }
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host "== $Name =="
    & $Action
}

switch ($Verb) {
    'status' {
        Invoke-UnityCli @('status')
    }
    'list' {
        Invoke-UnityCli @('list')
    }
    'compile' {
        Invoke-UnityCli @('editor', 'refresh', '--compile')
    }
    'clear-console' {
        Invoke-UnityCli @('console', '--clear')
    }
    'console' {
        Invoke-UnityCli @('console', '--lines', $Lines, '--type', $Filter)
    }
    'bootstrap' {
        Invoke-UnityCli @('menu', 'SM/Bootstrap/Prepare Observer Playable')
    }
    'test-edit' {
        Invoke-UnityCli @('test')
    }
    'test-play' {
        Invoke-UnityCli @('test', '--mode', 'PlayMode')
    }
    'report-town' {
        Invoke-UnityCli @('observer_contract_report', '--scene', 'town')
    }
    'report-battle' {
        Invoke-UnityCli @('observer_contract_report', '--scene', 'battle')
    }
    'smoke-observer' {
        Invoke-Step -Name 'compile' -Action { Invoke-UnityCli @('editor', 'refresh', '--compile') }
        Invoke-Step -Name 'clear-console' -Action { Invoke-UnityCli @('console', '--clear') }
        Invoke-Step -Name 'bootstrap' -Action { Invoke-UnityCli @('menu', 'SM/Bootstrap/Prepare Observer Playable') }
        Invoke-Step -Name 'report-town' -Action { Invoke-UnityCli @('observer_contract_report', '--scene', 'town') }
        Invoke-Step -Name 'report-battle' -Action { Invoke-UnityCli @('observer_contract_report', '--scene', 'battle') }
        Invoke-Step -Name 'console' -Action { Invoke-UnityCli @('console', '--lines', $Lines, '--type', $Filter) }
    }
    'exec' {
        if (-not $Dangerous) {
            throw "Raw unity-cli exec is blocked by default. Re-run with -Dangerous -Code '<C#>' only for explicitly approved read-first probes."
        }

        if ([string]::IsNullOrWhiteSpace($Code)) {
            throw "Pass -Code when using the exec verb."
        }

        Invoke-UnityCli @('exec', $Code)
    }
}

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
$unityCliReadyRetries = 5
$unityCliReadyDelaySeconds = 2

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
    $lastOutput = @()

    for ($attempt = 1; $attempt -le $unityCliReadyRetries; $attempt++) {
        $output = & $unityCli --project $projectRootForUnityCli @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        $outputText = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

        if ($exitCode -ne 0) {
            throw "unity-cli command failed with exit code $exitCode.`n$outputText"
        }

        if ($outputText -notmatch 'not responding') {
            if ($output.Count -gt 0) {
                $output | Write-Output
            }

            return
        }

        $lastOutput = $output

        if ($attempt -lt $unityCliReadyRetries) {
            Start-Sleep -Seconds $unityCliReadyDelaySeconds
        }
    }

    $lastOutputText = ($lastOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    throw "unity-cli connector is installed, but the Unity instance did not become ready after $unityCliReadyRetries attempts.`n$lastOutputText"
}

function Invoke-Status {
    Invoke-UnityCli @('status')
    Write-Host "Note: wrapper path fallback is active; bare 'unity-cli' is optional and may require a new shell session."
}

function Invoke-DirectCommandHint {
    Write-Host "Direct command example: `"$env:LOCALAPPDATA\unity-cli\unity-cli.exe`" --project `"$projectRootForUnityCli`" <command>"
}

function Invoke-StatusWithHints {
    try {
        Invoke-Status
    }
    catch {
        Invoke-DirectCommandHint
        throw
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
        Invoke-StatusWithHints
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

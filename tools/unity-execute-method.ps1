param(
    [Parameter(Mandatory = $true)]
    [string]$Method,
    [Parameter(Mandatory = $true)]
    [string]$LogFile,
    [Parameter(Mandatory = $true)]
    [string]$PhaseName,
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
$resolvedProjectRoot = (Resolve-Path $ProjectRoot).Path

function Resolve-UnityEditorPath {
    $versionFile = Join-Path $resolvedProjectRoot 'ProjectSettings\ProjectVersion.txt'
    if (Test-Path $versionFile) {
        $content = Get-Content $versionFile -Raw
        if ($content -match 'm_EditorVersion:\s*(\S+)') {
            $version = $Matches[1]
            $hubPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
            if (Test-Path $hubPath) {
                return $hubPath
            }
        }
    }

    $hubRoot = Join-Path $env:ProgramFiles 'Unity\Hub\Editor'
    if (Test-Path $hubRoot) {
        $latest = Get-ChildItem $hubRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($null -ne $latest) {
            $candidate = Join-Path $latest.FullName 'Editor\Unity.exe'
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    throw "Unity Editor executable not found. Check Unity Hub installation."
}

$unityExe = Resolve-UnityEditorPath
$logPath = Join-Path $resolvedProjectRoot $LogFile
$logDirectory = Split-Path $logPath -Parent

if (-not [string]::IsNullOrWhiteSpace($logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

if (Test-Path $logPath) {
    Remove-Item $logPath -Force
}

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $resolvedProjectRoot,
    '-executeMethod', $Method,
    '-logFile', $logPath,
    '-quit'
)

Write-Host "== batchmode executeMethod: $PhaseName =="
Write-Host "Unity: $unityExe"
Write-Host "Args: $($arguments -join ' ')"

$process = Start-Process -FilePath $unityExe -ArgumentList $arguments -PassThru -Wait -NoNewWindow
$exitCode = $process.ExitCode

Write-Host "LogFile: $logPath"

if ($exitCode -ne 0) {
    if (Test-Path $logPath) {
        Write-Host '== log tail =='
        Get-Content -Path $logPath -Tail 40 | Write-Host
    }

    throw "Unity batchmode executeMethod '$Method' exited with code $exitCode."
}

Write-Host "Batchmode executeMethod completed (exit code $exitCode)."

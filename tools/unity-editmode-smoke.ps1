param(
    [string]$UnityExe,
    [string]$ProjectPath = ".",
    [string]$LogFile = "Logs/editmode-tests.log",
    [string]$ResultsFile = "Logs/editmode-results.xml"
)

$ErrorActionPreference = 'Stop'

if (-not $UnityExe) {
    throw 'Pass -UnityExe with the local Unity editor path.'
}

New-Item -ItemType Directory -Force -Path (Split-Path $LogFile -Parent) | Out-Null

& $UnityExe \
  -batchmode \
  -nographics \
  -projectPath $ProjectPath \
  -runTests \
  -testPlatform EditMode \
  -testResults $ResultsFile \
  -logFile $LogFile \
  -quit

if ($LASTEXITCODE -ne 0) {
    throw "Unity EditMode tests failed with exit code $LASTEXITCODE"
}

Write-Host "EditMode tests completed. Log: $LogFile Results: $ResultsFile"

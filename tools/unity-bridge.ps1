param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('status', 'list', 'compile', 'clear-console', 'console', 'prepare-playable', 'repair-scenes', 'ensure-localization', 'quick-battle-smoke', 'seed-content', 'content-validate', 'balance-sweep-smoke', 'test-edit', 'test-play', 'test-batch-edit', 'test-batch-fast', 'report-town', 'report-battle', 'smoke-observer', 'capture-battle', 'loopd-slice', 'loopd-purekit', 'loopd-systemic', 'loopd-runlite', 'loopd-smoke', 'loopd-full', 'exec')]
    [string]$Verb,
    [int]$Lines = 30,
    [string]$Filter = 'error,warning,log',
    [string]$TestFilter,
    [string]$TestCategory,
    [string]$Code,
    [switch]$Dangerous
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path $PSScriptRoot -Parent
$projectRootForUnityCli = ((Resolve-Path $projectRoot).Path -replace '\\', '/')
Set-Location $projectRoot
$unityCliTransientRetries = 5
$unityCliTransientDelaySeconds = 2
$unityCliReadyPollRetries = 45
$unityCliLongReadyPollRetries = 300
$unityCliReadyPollDelaySeconds = 2
$unityCliPostDispatchDelaySeconds = 3
$unityCliTestResultsPollRetries = 480
$unityCliTestResultsPollDelaySeconds = 2
$unityCliReadyPatterns = @(
    '(?im):\s*ready\b',
    '(?im)\bUnity is ready\b'
)
$unityCliTransientPatterns = @(
    '(?im)\bnot responding\b',
    '(?im)\bconnection refused\b',
    '(?im)\bcontext deadline exceeded\b',
    '(?im)\bi/o timeout\b',
    '(?im)\brequest timed out\b',
    '(?im)\bconnection reset\b'
)

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

function ConvertTo-UnityCliOutputText {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Output
    )

    $text = ($Output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    return [regex]::Replace($text, '\x1b\[[0-9;]*m', '')
}

function Test-UnityCliOutputMatch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        if ($Text -match $pattern) {
            return $true
        }
    }

    return $false
}

function Get-UnityCliResult {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $unityCli = Resolve-UnityCliPath
    $output = & $unityCli --project $projectRootForUnityCli @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $outputText = ConvertTo-UnityCliOutputText -Output $output

    return [pscustomobject]@{
        Arguments  = $Arguments
        ExitCode   = $exitCode
        Output     = $output
        OutputText = $outputText
    }
}

function Write-UnityCliResult {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Result
    )

    if ($Result.Output.Count -gt 0) {
        $Result.Output | Write-Output
    }
}

function Wait-ForUnityCliReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Context,
        [int]$Retries = $unityCliReadyPollRetries,
        [int]$DelaySeconds = $unityCliReadyPollDelaySeconds
    )

    $lastResult = $null

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        $result = Get-UnityCliResult @('status')
        $lastResult = $result

        if ($result.ExitCode -eq 0 -and (Test-UnityCliOutputMatch -Text $result.OutputText -Patterns $unityCliReadyPatterns)) {
            Write-UnityCliResult -Result $result
            return
        }

        if ($attempt -lt $Retries) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    $lastOutputText = if ($null -ne $lastResult) { $lastResult.OutputText } else { '(no output)' }
    throw "unity-cli connector did not return to ready state after $Context.`n$lastOutputText"
}

function Invoke-UnityCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$WaitForReady,
        [string]$ReadyContext = 'command',
        [int]$InitialReadyDelaySeconds = 0,
        [int]$ReadyRetries = $unityCliReadyPollRetries
    )

    $lastResult = $null

    for ($attempt = 1; $attempt -le $unityCliTransientRetries; $attempt++) {
        $result = Get-UnityCliResult -Arguments $Arguments
        $lastResult = $result

        if ($result.ExitCode -ne 0) {
            throw "unity-cli command failed with exit code $($result.ExitCode).`n$($result.OutputText)"
        }

        $isTransient = Test-UnityCliOutputMatch -Text $result.OutputText -Patterns $unityCliTransientPatterns

        if ($isTransient) {
            if ($attempt -lt $unityCliTransientRetries) {
                Start-Sleep -Seconds $unityCliTransientDelaySeconds
                continue
            }

            throw "unity-cli connector remained busy after $unityCliTransientRetries attempts.`n$($result.OutputText)"
        }

        Write-UnityCliResult -Result $result

        if ($WaitForReady) {
            if ($InitialReadyDelaySeconds -gt 0) {
                Start-Sleep -Seconds $InitialReadyDelaySeconds
            }

            Wait-ForUnityCliReady -Context $ReadyContext -Retries $ReadyRetries
        }

        return
    }

    $lastOutputText = if ($null -ne $lastResult) { $lastResult.OutputText } else { '(no output)' }
    throw "unity-cli connector is installed, but the command did not stabilize after $unityCliTransientRetries attempts.`n$lastOutputText"
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

function Invoke-UnityMenuCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$MenuPath,
        [Parameter(Mandatory = $true)]
        [string]$ReadyContext,
        [int]$ReadyRetries = $unityCliLongReadyPollRetries
    )

    Invoke-UnityCli @('menu', $MenuPath) -WaitForReady -ReadyContext $ReadyContext -InitialReadyDelaySeconds $unityCliPostDispatchDelaySeconds -ReadyRetries $ReadyRetries
}

function Get-UnityTestResultsPath {
    $localAppDataRoot = Split-Path $env:LOCALAPPDATA -Parent
    $candidates = @(
        (Join-Path $localAppDataRoot 'LocalLow\DefaultCompany\survival-manager\TestResults.xml'),
        (Join-Path $localAppDataRoot 'LocalLow\DefaultCompany\Survival Manager\TestResults.xml')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $searchRoot = Join-Path $localAppDataRoot 'LocalLow'
    if (Test-Path $searchRoot) {
        $discovered = Get-ChildItem -Path $searchRoot -Filter 'TestResults.xml' -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like '*survival-manager*' } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($null -ne $discovered) {
            return $discovered.FullName
        }
    }

    return $candidates[0]
}

function Get-FileLastWriteTimeUtcOrMinValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        return (Get-Item $Path).LastWriteTimeUtc
    }

    return [datetime]::MinValue
}

function Wait-ForUnityTestResults {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [datetime]$PreviousWriteTimeUtc,
        [int]$Retries = $unityCliTestResultsPollRetries,
        [int]$DelaySeconds = $unityCliTestResultsPollDelaySeconds
    )

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        if (Test-Path $Path) {
            $item = Get-Item $Path
            if ($item.LastWriteTimeUtc -gt $PreviousWriteTimeUtc -and $item.Length -gt 0) {
                return $item.FullName
            }
        }

        if ($attempt -lt $Retries) {
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    throw "unity-cli test completed dispatch, but no fresh TestResults.xml was detected at $Path"
}

function Get-UnityTestSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    [xml]$xml = Get-Content -Path $Path
    $root = $xml.'test-run'
    $failedCases = @()

    if ($null -ne $root) {
        $nodes = $root.SelectNodes('//test-case[@result="Failed"]')
        foreach ($node in $nodes | Select-Object -First 5) {
            $messageNode = $node.SelectSingleNode('failure/message')
            $message = if ($null -ne $messageNode) { ($messageNode.InnerText -replace '\s+', ' ').Trim() } else { string.Empty }
            $failedCases += [pscustomobject]@{
                name    = $node.fullname
                message = $message
            }
        }
    }

    return [pscustomobject]@{
        result      = if ($null -ne $root) { [string]$root.result } else { 'Unknown' }
        total       = if ($null -ne $root) { [int]$root.total } else { 0 }
        passed      = if ($null -ne $root) { [int]$root.passed } else { 0 }
        failed      = if ($null -ne $root) { [int]$root.failed } else { 0 }
        skipped     = if ($null -ne $root) { [int]$root.skipped } else { 0 }
        duration    = if ($null -ne $root) { [double]$root.duration } else { 0.0 }
        resultsFile = $Path
        failures    = $failedCases
    }
}

function Write-UnityTestSummary {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Summary
    )

    $Summary | ConvertTo-Json -Depth 5 | Write-Output
}

function Invoke-UnityCliTest {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$ReadyContext,
        [string]$TestFilter
    )

    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $Arguments = @($Arguments + @('--filter', $TestFilter))
    }

    $resultsPath = Get-UnityTestResultsPath
    $previousWriteTimeUtc = Get-FileLastWriteTimeUtcOrMinValue -Path $resultsPath

    $result = Get-UnityCliResult -Arguments $Arguments
    if ($result.ExitCode -ne 0) {
        throw "unity-cli test command failed with exit code $($result.ExitCode).`n$($result.OutputText)"
    }

    $trimmedOutput = $result.OutputText.Trim()
    $isAsyncDispatch = $trimmedOutput -match '(?im)^\s*run_tests sent \(connection closed before response\)\s*$'

    Write-UnityCliResult -Result $result

    if (-not $isAsyncDispatch -and $trimmedOutput.StartsWith('{')) {
        try {
            $summary = $trimmedOutput | ConvertFrom-Json
            if ($summary.failed -gt 0) {
                throw "unity-cli test completed with $($summary.failed) failed tests."
            }

            return
        }
        catch {
            throw "unity-cli returned test output, but it could not be parsed reliably.`n$trimmedOutput"
        }
    }

    Start-Sleep -Seconds $unityCliPostDispatchDelaySeconds
    $freshResultsPath = Wait-ForUnityTestResults -Path $resultsPath -PreviousWriteTimeUtc $previousWriteTimeUtc
    Wait-ForUnityCliReady -Context $ReadyContext -Retries $unityCliLongReadyPollRetries
    $summary = Get-UnityTestSummary -Path $freshResultsPath
    Write-UnityTestSummary -Summary $summary

    if ($summary.failed -gt 0) {
        $firstFailure = $summary.failures | Select-Object -First 1
        if ($null -ne $firstFailure) {
            throw "unity-cli test completed with $($summary.failed) failed tests. First failure: $($firstFailure.name) :: $($firstFailure.message)"
        }

        throw "unity-cli test completed with $($summary.failed) failed tests."
    }
}

function Resolve-UnityEditorPath {
    # ProjectVersion.txt에서 에디터 버전을 읽어 Hub 설치 경로를 찾는다.
    $versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
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

    # 폴백: Hub 경로에서 가장 최근 버전 탐색
    $hubRoot = Join-Path $env:ProgramFiles 'Unity\Hub\Editor'
    if (Test-Path $hubRoot) {
        $latest = Get-ChildItem $hubRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($null -ne $latest) {
            $path = Join-Path $latest.FullName 'Editor\Unity.exe'
            if (Test-Path $path) {
                return $path
            }
        }
    }

    throw "Unity Editor executable not found. Check Unity Hub installation."
}

function Invoke-UnityBatchmodeTest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TestPlatform,
        [string]$TestFilter,
        [string]$TestCategory,
        [switch]$RunSynchronously
    )

    $unityExe = Resolve-UnityEditorPath
    $resultsPath = Join-Path $projectRoot 'TestResults-Batch.xml'
    $previousWriteTimeUtc = Get-FileLastWriteTimeUtcOrMinValue -Path $resultsPath

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $projectRoot,
        '-runTests',
        '-testPlatform', $TestPlatform,
        '-testResults', $resultsPath
    )

    # CRITICAL: -quit는 -runTests와 결합하면 안 된다 (테스트 완료 전 종료될 수 있음)

    if ($RunSynchronously -and $TestPlatform -eq 'EditMode') {
        # -runSynchronously는 EditMode 전용. [UnityTest]/[UnitySetUp]/[UnityTearDown] 필터링됨.
        $arguments += '-runSynchronously'
    }

    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $arguments += @('-testFilter', $TestFilter)
    }

    if (-not [string]::IsNullOrWhiteSpace($TestCategory)) {
        $arguments += @('-testCategory', $TestCategory)
    }

    Write-Host "== batchmode test: $TestPlatform =="
    Write-Host "Unity: $unityExe"
    Write-Host "Args: $($arguments -join ' ')"

    $process = Start-Process -FilePath $unityExe -ArgumentList $arguments -PassThru -Wait -NoNewWindow
    $exitCode = $process.ExitCode
    $hasFreshResults = $false

    if (Test-Path $resultsPath) {
        $resultsItem = Get-Item $resultsPath
        $hasFreshResults = $resultsItem.LastWriteTimeUtc -gt $previousWriteTimeUtc -and $resultsItem.Length -gt 0

        if ($hasFreshResults) {
            $summary = Get-UnityTestSummary -Path $resultsPath
            Write-UnityTestSummary -Summary $summary

            if ($summary.failed -gt 0) {
                throw "Batchmode test completed with $($summary.failed) failed tests."
            }
        }
    }

    if (-not $hasFreshResults) {
        if ($exitCode -ne 0) {
            throw "Unity batchmode test exited with code $exitCode and no fresh results file was produced. Another Unity instance may still hold the project lock."
        }

        throw "Unity batchmode test completed without writing a fresh results file at $resultsPath"
    }

    if ($exitCode -ne 0) {
        throw "Unity batchmode test exited with code $exitCode despite producing fresh results. Treating the run as failed."
    }

    Write-Host "Batchmode test completed (exit code $exitCode)."
}

try {
    switch ($Verb) {
        'status' {
            Invoke-StatusWithHints
        }
        'list' {
            Invoke-UnityCli @('list')
        }
        'compile' {
            Invoke-UnityCli @('editor', 'refresh', '--compile') -WaitForReady -ReadyContext 'compile'
        }
        'clear-console' {
            Invoke-UnityCli @('console', '--clear')
        }
        'console' {
            Invoke-UnityCli @('console', '--lines', $Lines, '--type', $Filter)
        }
        'prepare-playable' {
            Invoke-UnityMenuCommand -MenuPath 'SM/전체테스트' -ReadyContext 'prepare playable menu dispatch'
        }
        'repair-scenes' {
            Invoke-UnityMenuCommand -MenuPath 'SM/Internal/Recovery/Repair First Playable Scenes' -ReadyContext 'repair scenes menu dispatch'
        }
        'ensure-localization' {
            Invoke-UnityMenuCommand -MenuPath 'SM/Internal/Recovery/Ensure Localization Foundation' -ReadyContext 'ensure localization menu dispatch'
        }
        'quick-battle-smoke' {
            Invoke-UnityMenuCommand -MenuPath 'SM/전투테스트' -ReadyContext 'quick battle smoke menu dispatch'
        }
        'seed-content' {
            Invoke-UnityMenuCommand -MenuPath 'SM/Internal/Content/Generate Sample Content' -ReadyContext 'sample content generation'
        }
        'content-validate' {
            & pwsh -File (Join-Path $PSScriptRoot 'unity-execute-method.ps1') -Method 'SM.Editor.Validation.ValidationBatchEntryPoint.RunContentValidation' -LogFile 'Logs/content-validation-ci.log' -PhaseName 'content validation' -ProjectRoot $projectRoot
            if ($LASTEXITCODE -ne 0) {
                throw 'content validation executeMethod wrapper failed.'
            }
        }
        'balance-sweep-smoke' {
            & pwsh -File (Join-Path $PSScriptRoot 'unity-execute-method.ps1') -Method 'SM.Editor.Validation.ValidationBatchEntryPoint.RunBalanceSweepSmoke' -LogFile 'Logs/balance-sweep-ci.log' -PhaseName 'balance sweep smoke' -ProjectRoot $projectRoot
            if ($LASTEXITCODE -ne 0) {
                throw 'balance sweep smoke executeMethod wrapper failed.'
            }
        }
        'test-edit' {
            Invoke-UnityCliTest @('test') -ReadyContext 'edit mode test dispatch' -TestFilter $TestFilter
        }
        'test-play' {
            Invoke-UnityCliTest @('test', '--mode', 'PlayMode') -ReadyContext 'play mode test dispatch' -TestFilter $TestFilter
        }
        'test-batch-edit' {
            # batchmode EditMode 테스트: GUI 없이 독립 프로세스로 실행.
            # -runSynchronously 사용 ([UnityTest] 제외, 순수 NUnit [Test]만 실행).
            Invoke-UnityBatchmodeTest -TestPlatform 'EditMode' -TestFilter $TestFilter -TestCategory $TestCategory -RunSynchronously
        }
        'test-batch-fast' {
            # FastUnit 카테고리만 batchmode로 실행 (Resources.LoadAll 없는 테스트).
            $effectiveCategory = if (-not [string]::IsNullOrWhiteSpace($TestCategory)) { $TestCategory } else { 'FastUnit' }
            Invoke-UnityBatchmodeTest -TestPlatform 'EditMode' -TestFilter $TestFilter -TestCategory $effectiveCategory -RunSynchronously
        }
        'report-town' {
            Invoke-UnityCli @('observer_contract_report', '--scene', 'town')
        }
        'report-battle' {
            Invoke-UnityCli @('observer_contract_report', '--scene', 'battle')
        }
        'capture-battle' {
            $markerPath = Join-Path $projectRoot 'Captures\.last_capture'
            $previousMarker = Get-FileLastWriteTimeUtcOrMinValue -Path $markerPath
            Invoke-UnityMenuCommand -MenuPath 'SM/Internal/Capture/Battle Scene' -ReadyContext 'battle scene capture'
            for ($attempt = 1; $attempt -le 60; $attempt++) {
                if (Test-Path $markerPath) {
                    $current = (Get-Item $markerPath).LastWriteTimeUtc
                    if ($current -gt $previousMarker) {
                        $latest = Join-Path $projectRoot 'Captures\battle_latest.png'
                        if (Test-Path $latest) {
                            Write-Host "Capture saved: $latest"
                            break
                        }
                    }
                }
                Start-Sleep -Milliseconds 500
            }
        }
        'smoke-observer' {
            Invoke-Step -Name 'compile' -Action { Invoke-UnityCli @('editor', 'refresh', '--compile') -WaitForReady -ReadyContext 'compile' }
            Invoke-Step -Name 'clear-console' -Action { Invoke-UnityCli @('console', '--clear') }
            Invoke-Step -Name 'prepare-playable' -Action { Invoke-UnityMenuCommand -MenuPath 'SM/전체테스트' -ReadyContext 'prepare playable menu dispatch' }
            Invoke-Step -Name 'report-town' -Action { Invoke-UnityCli @('observer_contract_report', '--scene', 'town') }
            Invoke-Step -Name 'report-battle' -Action { Invoke-UnityCli @('observer_contract_report', '--scene', 'battle') }
            Invoke-Step -Name 'console' -Action { Invoke-UnityCli @('console', '--lines', $Lines, '--type', $Filter) }
        }
        'loopd-slice' {
            Invoke-UnityCli @('loop_d_balance_report', '--mode', 'slice')
        }
        'loopd-purekit' {
            Invoke-UnityCli @('loop_d_balance_report', '--mode', 'purekit', '--smoke', 'true', '--fail_on_error', 'true')
        }
        'loopd-systemic' {
            Invoke-UnityCli @('loop_d_balance_report', '--mode', 'systemic', '--smoke', 'true', '--fail_on_error', 'true')
        }
        'loopd-runlite' {
            Invoke-UnityCli @('loop_d_balance_report', '--mode', 'runlite', '--smoke', 'true', '--fail_on_error', 'true')
        }
        'loopd-smoke' {
            Invoke-UnityCli @('loop_d_balance_report', '--mode', 'smoke', '--fail_on_error', 'true')
        }
        'loopd-full' {
            Invoke-UnityCli @('loop_d_balance_report', '--mode', 'full', '--fail_on_error', 'true')
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
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}

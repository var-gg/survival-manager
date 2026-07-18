[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'h100-live-coldstart.config.json'),
    [string]$OutputDirectory = 'Logs/h100-live-coldstart',
    [switch]$DryRunSmoke,
    [switch]$LiveSmoke,
    [string]$OwnerApprovalPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$invariantCulture = [Globalization.CultureInfo]::InvariantCulture
$utf8WithoutBom = [Text.UTF8Encoding]::new($false)
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$executeMethod = Join-Path $PSScriptRoot 'unity-execute-method.ps1'
$pwshPath = (Get-Command pwsh -ErrorAction Stop).Source
$codexPath = (Get-Command codex -ErrorAction Stop).Source
$hostileCultures = @('tr-TR', 'de-DE', 'ja-JP')
$environmentNames = @(
    'SM_H100_BATTLE_COUNT',
    'SM_H100_CAMPAIGN_COUNT',
    'SM_H100_REPLAY_COPIES',
    'SM_H100_SEED_BASE',
    'SM_H100_SITE_SAFETY',
    'SM_H100_MAX_BATTLE_STEPS',
    'SM_H100_WRITE_CSV',
    'SM_H100_OUTPUT',
    'SM_H100_POLICY',
    'SM_H100_SEALED_OUTPUT',
    'SM_H100_TRACE_PATH',
    'SM_H100_REPLAY_RESULT_DIR',
    'SM_H100_FORCE_CULTURE',
    'SM_H100_REPLAY_RESULT_DIRS',
    'SM_H100_LIVE_RUN_ID',
    'SM_H100_LIVE_EXCHANGE_DIR',
    'SM_H100_PROMPT_TEMPLATE_ID',
    'SM_H100_PROMPT_TEMPLATE',
    'SM_H100_PROMPT_TEMPLATE_FILE',
    'SM_H100_COLD_START_BRIEFING',
    'SM_H100_COLD_START_BRIEFING_FILE',
    'SM_H100_MODEL_SNAPSHOT',
    'SM_H100_DECODING_CONFIG',
    'SM_H100_DECISION_TIMEOUT_SECONDS',
    'SM_H100_RUN_REPORT_TIMEOUT_SECONDS',
    'SM_H100_POLL_INTERVAL_MS',
    'SM_H100_PROMPT_ARCHIVE_DIR',
    'SM_H100_TRACE_PATHS',
    'SM_H100_LEDGER_PATHS',
    'SM_H100_EXPECTED_PROMPT_SCHEMA_HASH',
    'SM_H100_OWNER_APPROVAL_PATH'
)

function Format-Invariant {
    param([Parameter(Mandatory = $true)][object]$Value)

    if ($Value -is [IFormattable]) {
        return $Value.ToString($null, $invariantCulture)
    }

    return $Value.ToString()
}

function Resolve-ProjectPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [switch]$MustExist,
        [switch]$AllowDirectory
    )

    $candidate = if ([IO.Path]::IsPathRooted($Path)) {
        [IO.Path]::GetFullPath($Path)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $projectRoot $Path))
    }
    $rootPrefix = $projectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path must stay inside the project root: $candidate"
    }
    if ($MustExist -and -not (Test-Path -LiteralPath $candidate)) {
        throw "Required path does not exist: $candidate"
    }
    if ($MustExist -and -not $AllowDirectory -and -not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Required file does not exist: $candidate"
    }

    return $candidate
}

function Convert-ToProjectRelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [IO.Path]::GetRelativePath($projectRoot, [IO.Path]::GetFullPath($Path)).Replace('\', '/')
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowEmptyString()][Parameter(Mandatory = $true)][string]$Content
    )

    $directory = Split-Path $Path -Parent
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Content, $utf8WithoutBom)
}

function Write-JsonAtomic {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value,
        [switch]$Immutable
    )

    $finalPath = [IO.Path]::GetFullPath($Path)
    if ($Immutable -and (Test-Path -LiteralPath $finalPath)) {
        throw "Immutable JSON already exists: $finalPath"
    }
    $directory = Split-Path $finalPath -Parent
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $temporaryPath = "$finalPath.tmp"
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
    $json = $Value | ConvertTo-Json -Depth 32
    [IO.File]::WriteAllText($temporaryPath, $json + "`n", $utf8WithoutBom)
    if ($Immutable) {
        [IO.File]::Move($temporaryPath, $finalPath)
    }
    else {
        [IO.File]::Move($temporaryPath, $finalPath, $true)
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Get-LengthPrefixedStableHash {
    param([Parameter(Mandatory = $true)][object[]]$Parts)

    $stream = [IO.MemoryStream]::new()
    try {
        foreach ($part in $Parts) {
            $bytes = if ($part -is [byte[]]) {
                $part
            }
            else {
                $utf8WithoutBom.GetBytes([string]$part)
            }
            $lengthBytes = [Text.Encoding]::ASCII.GetBytes($bytes.Length.ToString($invariantCulture))
            $stream.Write($lengthBytes, 0, $lengthBytes.Length)
            $stream.WriteByte([byte][char]':')
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.WriteByte([byte][char]'|')
        }

        [Numerics.BigInteger]$hash = 14695981039346656037
        [Numerics.BigInteger]$prime = 1099511628211
        [Numerics.BigInteger]$modulus = [Numerics.BigInteger]::Pow(2, 64)
        foreach ($value in $stream.ToArray()) {
            $hash = (($hash -bxor [Numerics.BigInteger]$value) * $prime) % $modulus
        }
        return ([uint64]$hash).ToString('x16', $invariantCulture)
    }
    finally {
        $stream.Dispose()
    }
}

function Get-PromptSchemaHash {
    param(
        [Parameter(Mandatory = $true)][string]$PromptTemplateId,
        [Parameter(Mandatory = $true)][string]$PromptTemplate,
        [Parameter(Mandatory = $true)][string]$ColdStartBriefing,
        [Parameter(Mandatory = $true)][string]$ModelSnapshotId,
        [Parameter(Mandatory = $true)][string]$DecodingConfig
    )

    return Get-LengthPrefixedStableHash -Parts @(
        'LlmPromptManifestV1',
        $PromptTemplateId,
        $PromptTemplate,
        'LlmWireV1',
        $ColdStartBriefing,
        $ModelSnapshotId,
        $DecodingConfig
    )
}

function Save-Environment {
    $snapshot = @{}
    foreach ($name in $environmentNames) {
        $existing = Get-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        $snapshot[$name] = if ($null -eq $existing) {
            [pscustomobject]@{ Exists = $false; Value = $null }
        }
        else {
            [pscustomobject]@{ Exists = $true; Value = $existing.Value }
        }
    }
    return $snapshot
}

function Restore-Environment {
    param([Parameter(Mandatory = $true)][hashtable]$Snapshot)

    foreach ($name in $environmentNames) {
        $saved = $Snapshot[$name]
        if ($null -ne $saved -and $saved.Exists) {
            Set-Item -LiteralPath "Env:$name" -Value $saved.Value
        }
        else {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
    }
}

function Set-EnvironmentBundle {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$Values)

    foreach ($name in $environmentNames) {
        Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
    }
    foreach ($entry in $Values.GetEnumerator()) {
        if ($null -ne $entry.Value) {
            Set-Item -LiteralPath "Env:$($entry.Key)" -Value ([string]$entry.Value)
        }
    }
}

function Start-CapturedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$StdoutPath,
        [Parameter(Mandatory = $true)][string]$StderrPath,
        [switch]$CloseStandardInput,
        [string]$StandardInputText
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.RedirectStandardInput = $CloseStandardInput.IsPresent -or -not [string]::IsNullOrEmpty($StandardInputText)
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Failed to start process: $FilePath"
    }
    # Drain stdout/stderr asynchronously BEFORE writing the (large) stdin payload so a full stdout
    # pipe cannot deadlock against a blocking stdin write.
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not [string]::IsNullOrEmpty($StandardInputText)) {
        $process.StandardInput.Write($StandardInputText)
        $process.StandardInput.Close()
    }
    elseif ($CloseStandardInput) {
        $process.StandardInput.Close()
    }

    return [pscustomobject]@{
        Process = $process
        StdoutTask = $stdoutTask
        StderrTask = $stderrTask
        StdoutPath = $StdoutPath
        StderrPath = $StderrPath
    }
}

function Complete-CapturedProcess {
    param(
        [Parameter(Mandatory = $true)][object]$Handle,
        [int]$TimeoutMilliseconds = -1,
        [string]$TimeoutLabel = 'process'
    )

    $process = $Handle.Process
    $completed = if ($TimeoutMilliseconds -lt 0) {
        $process.WaitForExit()
        $true
    }
    else {
        $process.WaitForExit($TimeoutMilliseconds)
    }
    if (-not $completed) {
        try { $process.Kill($true) } catch { }
        $process.WaitForExit()
    }

    $stdout = $Handle.StdoutTask.GetAwaiter().GetResult()
    $stderr = $Handle.StderrTask.GetAwaiter().GetResult()
    Write-Utf8File -Path $Handle.StdoutPath -Content $stdout
    Write-Utf8File -Path $Handle.StderrPath -Content $stderr
    $exitCode = $process.ExitCode
    $process.Dispose()
    if (-not $completed) {
        throw "$TimeoutLabel exceeded $TimeoutMilliseconds ms."
    }

    return [pscustomobject]@{ ExitCode = $exitCode; Stdout = $stdout; Stderr = $stderr }
}

function Stop-CapturedProcess {
    param([object]$Handle)

    if ($null -eq $Handle) { return }
    try {
        if (-not $Handle.Process.HasExited) {
            $Handle.Process.Kill($true)
            $Handle.Process.WaitForExit()
        }
    }
    catch { }
    try { $null = Complete-CapturedProcess -Handle $Handle } catch { }
}

function Assert-UnityEditorClosed {
    $matches = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            $_.CommandLine.IndexOf($projectRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
            $_.CommandLine.IndexOf('-batchmode', [StringComparison]::OrdinalIgnoreCase) -lt 0
        })
    if ($matches.Count -gt 0) {
        throw "Unity Editor must be closed for this project. Active PIDs: $($matches.ProcessId -join ',')"
    }
}

function Start-UnityMethod {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$LogFile,
        [Parameter(Mandatory = $true)][string]$PhaseName,
        [Parameter(Mandatory = $true)][string]$DriverLogPrefix
    )

    $arguments = @(
        '-NoProfile',
        '-File', $executeMethod,
        '-Method', $Method,
        '-LogFile', $LogFile,
        '-PhaseName', $PhaseName,
        '-ProjectRoot', $projectRoot
    )
    return Start-CapturedProcess -FilePath $pwshPath -Arguments $arguments -WorkingDirectory $projectRoot `
        -StdoutPath "$DriverLogPrefix.stdout.log" -StderrPath "$DriverLogPrefix.stderr.log" -CloseStandardInput
}

function Invoke-UnityMethod {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$LogFile,
        [Parameter(Mandatory = $true)][string]$PhaseName,
        [Parameter(Mandatory = $true)][string]$DriverLogPrefix,
        [int]$TimeoutSeconds = 3600,
        [switch]$AllowFailure
    )

    $handle = Start-UnityMethod -Method $Method -LogFile $LogFile -PhaseName $PhaseName -DriverLogPrefix $DriverLogPrefix
    $result = Complete-CapturedProcess -Handle $handle -TimeoutMilliseconds ($TimeoutSeconds * 1000) -TimeoutLabel $PhaseName
    if ($result.ExitCode -ne 0 -and -not $AllowFailure) {
        throw "$PhaseName failed with exit code $($result.ExitCode.ToString($invariantCulture))."
    }
    return $result
}

function Assert-SeamEqual {
    param(
        [Parameter(Mandatory = $true)][object]$Expected,
        [Parameter(Mandatory = $true)][object]$Actual,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if ([int]$Expected.decision_index -ne [int]$Actual.decision_index -or
        [string]$Expected.seam_type -cne [string]$Actual.seam_type -or
        [int]$Expected.ordinal -ne [int]$Actual.ordinal) {
        throw "$Label seam_key mismatch."
    }
}

function Read-ExchangeRequest {
    param([Parameter(Mandatory = $true)][string]$Path)

    $request = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    if ([string]$request.schema_version -cne 'SealedLlmExchangeRequestV1') {
        throw "Unsupported exchange request schema: $($request.schema_version)"
    }
    if ([string]::IsNullOrWhiteSpace([string]$request.request_canonical_hash)) {
        throw "Exchange request hash is missing: $Path"
    }
    if ([IO.Path]::GetFileName([string]$request.prompt_file) -cne [string]$request.prompt_file) {
        throw "Exchange prompt_file is not a leaf filename: $($request.prompt_file)"
    }
    if ([int]$request.attempt_limit -ne 3) {
        throw "Exchange attempt_limit must be 3: $($request.attempt_limit)"
    }
    return $request
}

function Read-ExchangeReject {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Request,
        [Parameter(Mandatory = $true)][int]$ExpectedNextAttempt
    )

    $reject = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    if ([string]$reject.schema_version -cne 'SealedLlmExchangeRejectV1') {
        throw "Unsupported exchange reject schema: $($reject.schema_version)"
    }
    Assert-SeamEqual -Expected $Request.seam_key -Actual $reject.seam_key -Label 'reject'
    if ([string]$reject.request_canonical_hash -cne [string]$Request.request_canonical_hash) {
        throw 'Reject request_canonical_hash mismatch.'
    }
    if ([string]$reject.reason_kind -cnotin @('strict_parse', 'action_decode')) {
        throw "Reject reason_kind is invalid: $($reject.reason_kind)"
    }
    if ([int]$reject.next_attempt -ne $ExpectedNextAttempt) {
        throw "Reject next_attempt mismatch: expected=$ExpectedNextAttempt actual=$($reject.next_attempt)"
    }
    if ([string]::IsNullOrWhiteSpace([string]$reject.error_text)) {
        throw 'Reject error_text is empty.'
    }
    return $reject
}

function Write-ExchangeResponse {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Request,
        [Parameter(Mandatory = $true)][string]$AgentKind,
        [AllowEmptyString()][Parameter(Mandatory = $true)][string]$RawResponseJson
    )

    $envelope = [ordered]@{
        schema_version = 'SealedLlmExchangeResponseV1'
        seam_key = [ordered]@{
            decision_index = [int]$Request.seam_key.decision_index
            seam_type = [string]$Request.seam_key.seam_type
            ordinal = [int]$Request.seam_key.ordinal
        }
        request_canonical_hash = [string]$Request.request_canonical_hash
        agent_kind = $AgentKind
        raw_response_json = $RawResponseJson
    }
    Write-JsonAtomic -Path $Path -Value $envelope -Immutable
}

function Get-EvidenceFactIds {
    param([Parameter(Mandatory = $true)][string]$Prompt)

    $map = [regex]::Match($Prompt, '\"EvidenceFactIdsBySignal\":\{(?<body>[^}]*)\}')
    if (-not $map.Success) {
        throw 'Scripted stub prompt omitted EvidenceFactIdsBySignal.'
    }
    $values = @([regex]::Matches($map.Groups['body'].Value, ':\"(?<value>[^\"]+)\"') |
        ForEach-Object { $_.Groups['value'].Value } |
        Sort-Object -Unique)
    if ($values.Count -eq 0) {
        throw 'Scripted stub prompt evidence map was empty.'
    }
    return $values
}

function New-ScriptedStubResponse {
    param(
        [Parameter(Mandatory = $true)][object]$Request,
        [Parameter(Mandatory = $true)][string]$Prompt
    )

    if ([string]$Request.seam_key.seam_type -ceq 'run_report') {
        return ([ordered]@{
            desire_retrospective = 'scripted stub formed a visible test desire'
            payoff_or_near_miss = 'scripted stub observed a visible payoff or near miss'
            next_concept = 'scripted stub would try another visible concept'
            complaints = @()
            evaluation_sentences = @()
            retry_intent = 'retry with another visible concept'
        } | ConvertTo-Json -Compress -Depth 12)
    }

    $selectedAction = $null
    if ($null -ne $Request.deployment_action_space) {
        $space = $Request.deployment_action_space
        $anchors = @($space.available_anchor_ids | Sort-Object)
        $heroes = @($space.available_hero_ids | Sort-Object)
        $capacity = [int]$space.deploy_capacity
        if ($anchors.Count -lt $capacity -or $heroes.Count -lt $capacity) {
            throw 'Scripted stub deployment action space is undersized.'
        }
        $parts = for ($index = 0; $index -lt $capacity; $index++) {
            "$($anchors[$index])=$($heroes[$index])"
        }
        $selectedAction = $parts -join ';'
    }
    else {
        $legal = @($Request.legal_action_keys)
        if ($legal.Count -eq 0) {
            throw 'Scripted stub decision request has no legal action.'
        }
        $selectedAction = [string]$legal[0]
    }

    return ([ordered]@{
        selected_action = $selectedAction
        declared_intent = [ordered]@{
            intent_id = 'scripted-stub-intent'
            track_token_ids = @()
            expected_payoff = 'exercise the selected visible action'
            evidence_fact_ids = @(Get-EvidenceFactIds -Prompt $Prompt)
            next_acquisition_plan = 'inspect the next visible decision'
            allowed_substitutions = @()
            pivot_conditions = @('pivot if visible evidence changes')
            confidence = 1.0
        }
        intent_ref = 'scripted-stub-intent'
        build_hypotheses = @()
    } | ConvertTo-Json -Compress -Depth 12)
}

function New-RejectRetryPrompt {
    param([Parameter(Mandatory = $true)][object]$Reject)

    return @"
Your previous response was rejected by the strict game parser.
Return exactly one corrected JSON object and nothing else. Do not use tools, files, web access, or external knowledge. Keep reasoning grounded only in the player-visible messages already present in this campaign.
reason_kind=$($Reject.reason_kind)
parser_error=$($Reject.error_text)
"@
}

function Get-SessionIdFromEvents {
    param([Parameter(Mandatory = $true)][string]$Path)

    foreach ($line in [IO.File]::ReadLines($Path, $utf8WithoutBom)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $event = $line | ConvertFrom-Json
        if ([string]$event.type -ceq 'thread.started' -and -not [string]::IsNullOrWhiteSpace([string]$event.thread_id)) {
            return [string]$event.thread_id
        }
    }
    throw "Codex transcript did not expose thread.started/thread_id: $Path"
}

function Invoke-CodexAdapter {
    param(
        [Parameter(Mandatory = $true)][object]$Binding,
        [Parameter(Mandatory = $true)][object]$SessionState,
        [Parameter(Mandatory = $true)][string]$SandboxDirectory,
        [Parameter(Mandatory = $true)][string]$TranscriptDirectory,
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)][int]$Attempt,
        [Parameter(Mandatory = $true)][string]$Prompt,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    $stem = "$Prefix-a$($Attempt.ToString($invariantCulture))"
    $eventsPath = Join-Path $TranscriptDirectory "events-$stem.jsonl"
    $stderrPath = Join-Path $TranscriptDirectory "stderr-$stem.log"
    $responsePath = Join-Path $TranscriptDirectory "response-raw-$stem.txt"
    $common = @(
        '-m', [string]$Binding.model,
        '-c', "model_reasoning_effort=$($Binding.reasoning_effort)",
        '--json',
        '--skip-git-repo-check',
        '-o', $responsePath
    )
    $arguments = if ([string]::IsNullOrWhiteSpace([string]$SessionState.SessionId)) {
        @('exec', '-C', $SandboxDirectory, '-s', [string]$Binding.sandbox) + $common
    }
    else {
        @('exec', 'resume', [string]$SessionState.SessionId) + $common
    }

    # Observation prompts exceed the ~32KB Windows command-line limit, so the prompt is delivered on
    # stdin. codex reads instructions from stdin when no positional PROMPT argument is supplied.
    $handle = Start-CapturedProcess -FilePath $codexPath -Arguments $arguments -WorkingDirectory $SandboxDirectory `
        -StdoutPath $eventsPath -StderrPath $stderrPath -StandardInputText $Prompt
    $result = Complete-CapturedProcess -Handle $handle -TimeoutMilliseconds ($TimeoutSeconds * 1000) `
        -TimeoutLabel "codex $Prefix attempt $Attempt"
    if ($result.ExitCode -ne 0) {
        throw "Codex adapter failed for $Prefix attempt $Attempt with exit code $($result.ExitCode.ToString($invariantCulture))."
    }
    if (-not (Test-Path -LiteralPath $responsePath -PathType Leaf)) {
        throw "Codex adapter did not write a final response: $responsePath"
    }
    if ([string]::IsNullOrWhiteSpace([string]$SessionState.SessionId)) {
        $SessionState.SessionId = Get-SessionIdFromEvents -Path $eventsPath
    }
    $SessionState.InvocationCount = [int]$SessionState.InvocationCount + 1
    return [IO.File]::ReadAllText($responsePath, $utf8WithoutBom)
}

function Invoke-ScriptedStubAdapter {
    param(
        [Parameter(Mandatory = $true)][object]$Request,
        [Parameter(Mandatory = $true)][string]$Prompt,
        [Parameter(Mandatory = $true)][string]$TranscriptDirectory,
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)][int]$Attempt,
        [Parameter(Mandatory = $true)][object]$SessionState
    )

    $stem = "$Prefix-a$($Attempt.ToString($invariantCulture))"
    $raw = New-ScriptedStubResponse -Request $Request -Prompt $Prompt
    $event = [ordered]@{
        type = 'item.completed'
        item = [ordered]@{ id = $stem; type = 'agent_message'; text = $raw }
    } | ConvertTo-Json -Compress -Depth 8
    Write-Utf8File -Path (Join-Path $TranscriptDirectory "events-$stem.jsonl") -Content ($event + "`n")
    Write-Utf8File -Path (Join-Path $TranscriptDirectory "stderr-$stem.log") -Content ''
    Write-Utf8File -Path (Join-Path $TranscriptDirectory "response-raw-$stem.txt") -Content $raw
    $SessionState.InvocationCount = [int]$SessionState.InvocationCount + 1
    return $raw
}

function Invoke-PlayerAdapter {
    param(
        [Parameter(Mandatory = $true)][object]$Binding,
        [Parameter(Mandatory = $true)][object]$SessionState,
        [Parameter(Mandatory = $true)][string]$SandboxDirectory,
        [Parameter(Mandatory = $true)][string]$TranscriptDirectory,
        [Parameter(Mandatory = $true)][string]$Prefix,
        [Parameter(Mandatory = $true)][int]$Attempt,
        [Parameter(Mandatory = $true)][string]$Prompt,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    switch -CaseSensitive ([string]$Binding.mechanism) {
        'codex-exec' {
            return Invoke-CodexAdapter -Binding $Binding -SessionState $SessionState `
                -SandboxDirectory $SandboxDirectory -TranscriptDirectory $TranscriptDirectory `
                -Prefix $Prefix -Attempt $Attempt -Prompt $Prompt -TimeoutSeconds $TimeoutSeconds
        }
        default {
            throw "No player adapter is implemented for mechanism '$($Binding.mechanism)'. Add it at Invoke-PlayerAdapter without changing the campaign loop."
        }
    }
}

function Service-ExchangeOnce {
    param(
        [Parameter(Mandatory = $true)][string]$ExchangeDirectory,
        [Parameter(Mandatory = $true)][string]$TranscriptDirectory,
        [Parameter(Mandatory = $true)][object]$Binding,
        [Parameter(Mandatory = $true)][object]$SessionState,
        [Parameter(Mandatory = $true)][string]$SandboxDirectory,
        [Parameter(Mandatory = $true)][bool]$UseScriptedStub,
        [Parameter(Mandatory = $true)][int]$DecisionTimeoutSeconds,
        [Parameter(Mandatory = $true)][int]$RunReportTimeoutSeconds
    )

    $serviced = 0
    $requestPaths = @(Get-ChildItem -LiteralPath $ExchangeDirectory -Filter '*.request.json' -File -ErrorAction SilentlyContinue |
        Sort-Object Name)
    foreach ($requestPath in $requestPaths) {
        $request = Read-ExchangeRequest -Path $requestPath.FullName
        $prefix = $requestPath.Name.Substring(0, $requestPath.Name.Length - '.request.json'.Length)
        $promptPath = Join-Path $ExchangeDirectory ([string]$request.prompt_file)
        if (-not (Test-Path -LiteralPath $promptPath -PathType Leaf)) {
            throw "Exchange prompt is missing: $promptPath"
        }
        $prompt = [IO.File]::ReadAllText($promptPath, $utf8WithoutBom)
        for ($attempt = 1; $attempt -le [int]$request.attempt_limit; $attempt++) {
            $responsePath = Join-Path $ExchangeDirectory "$prefix.a$attempt.response.json"
            if (Test-Path -LiteralPath $responsePath) { continue }

            $message = $prompt
            if ($attempt -gt 1) {
                $rejectPath = Join-Path $ExchangeDirectory "$prefix.a$($attempt - 1).reject.json"
                if (-not (Test-Path -LiteralPath $rejectPath -PathType Leaf)) { break }
                $reject = Read-ExchangeReject -Path $rejectPath -Request $request -ExpectedNextAttempt $attempt
                $message = New-RejectRetryPrompt -Reject $reject
            }

            $raw = if ($UseScriptedStub) {
                Invoke-ScriptedStubAdapter -Request $request -Prompt $message -TranscriptDirectory $TranscriptDirectory `
                    -Prefix $prefix -Attempt $attempt -SessionState $SessionState
            }
            else {
                $timeout = if ([string]$request.seam_key.seam_type -ceq 'run_report') {
                    $RunReportTimeoutSeconds
                }
                else {
                    $DecisionTimeoutSeconds
                }
                Invoke-PlayerAdapter -Binding $Binding -SessionState $SessionState -SandboxDirectory $SandboxDirectory `
                    -TranscriptDirectory $TranscriptDirectory -Prefix $prefix -Attempt $attempt -Prompt $message `
                    -TimeoutSeconds $timeout
            }
            $agentKind = if ($UseScriptedStub) { 'scripted-stub' } else { [string]$Binding.agent_kind }
            Write-ExchangeResponse -Path $responsePath -Request $request -AgentKind $agentKind -RawResponseJson $raw
            $serviced++
            break
        }
    }
    return $serviced
}

function Test-TranscriptTaint {
    param([Parameter(Mandatory = $true)][string]$TranscriptDirectory)

    $findings = @()
    $eventCount = 0
    $files = @(Get-ChildItem -LiteralPath $TranscriptDirectory -Filter 'events-*.jsonl' -File -ErrorAction SilentlyContinue |
        Sort-Object Name)
    foreach ($file in $files) {
        $lineNumber = 0
        foreach ($line in [IO.File]::ReadLines($file.FullName, $utf8WithoutBom)) {
            $lineNumber++
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $eventCount++
            try {
                $event = $line | ConvertFrom-Json
            }
            catch {
                $findings += [pscustomobject]@{
                    file = $file.Name; line = $lineNumber; event_type = 'invalid_json'; item_type = 'invalid_json'
                }
                continue
            }
            $eventType = [string]$event.type
            $itemType = if ($null -ne $event.PSObject.Properties['item'] -and $null -ne $event.item) {
                [string]$event.item.type
            }
            else { '' }
            $joined = "$eventType|$itemType"
            if ($joined -match '(?i)(command_execution|function_call|mcp_tool_call|custom_tool_call|tool_call|file_(access|read|write|search)|web_search|computer_)') {
                $findings += [pscustomobject]@{
                    file = $file.Name; line = $lineNumber; event_type = $eventType; item_type = $itemType
                }
            }
        }
    }

    return [pscustomobject]@{
        status = if ($findings.Count -eq 0) { 'clean' } else { 'tainted' }
        event_file_count = $files.Count
        event_count = $eventCount
        tool_use_events = $findings
    }
}

function New-PlayerSandbox {
    param(
        [Parameter(Mandatory = $true)][string]$CohortId,
        [Parameter(Mandatory = $true)][int]$Slot,
        [Parameter(Mandatory = $true)][int]$Attempt
    )

    $temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'h100-player'))
    $sandbox = [IO.Path]::GetFullPath((Join-Path $temporaryRoot (
        "$CohortId/slot-$($Slot.ToString('D2', $invariantCulture))-attempt-$($Attempt.ToString('D2', $invariantCulture))-$([Guid]::NewGuid().ToString('N'))")))
    $rootPrefix = $projectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($sandbox.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Player sandbox resolved inside the repository: $sandbox"
    }
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    if (@(Get-ChildItem -LiteralPath $sandbox -Force).Count -ne 0) {
        throw "Player sandbox is not empty: $sandbox"
    }
    return $sandbox
}

function Get-RunEnvironmentBundle {
    param(
        [Parameter(Mandatory = $true)][int]$Seed,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$ExchangePath,
        [Parameter(Mandatory = $true)][object]$Runtime
    )

    return [ordered]@{
        SM_H100_BATTLE_COUNT = '1'
        SM_H100_CAMPAIGN_COUNT = '1'
        SM_H100_REPLAY_COPIES = '2'
        SM_H100_SEED_BASE = $Seed.ToString($invariantCulture)
        SM_H100_SITE_SAFETY = ([int]$Runtime.Config.campaign.site_safety).ToString($invariantCulture)
        SM_H100_MAX_BATTLE_STEPS = ([int]$Runtime.Config.campaign.max_battle_steps).ToString($invariantCulture)
        SM_H100_WRITE_CSV = 'false'
        SM_H100_POLICY = [string]$Runtime.Config.campaign.policy_id
        SM_H100_SEALED_OUTPUT = $OutputPath
        SM_H100_LIVE_RUN_ID = $RunId
        SM_H100_LIVE_EXCHANGE_DIR = $ExchangePath
        SM_H100_PROMPT_TEMPLATE_ID = [string]$Runtime.Config.prompt_manifest.prompt_template_id
        SM_H100_PROMPT_TEMPLATE_FILE = $Runtime.PromptTemplatePath
        SM_H100_COLD_START_BRIEFING_FILE = $Runtime.ColdStartBriefingPath
        SM_H100_MODEL_SNAPSHOT = $Runtime.ModelSnapshotId
        SM_H100_DECODING_CONFIG = $Runtime.DecodingConfig
        SM_H100_DECISION_TIMEOUT_SECONDS = ([int]$Runtime.Config.constants.decision_timeout_seconds).ToString($invariantCulture)
        SM_H100_RUN_REPORT_TIMEOUT_SECONDS = ([int]$Runtime.Config.constants.run_report_timeout_seconds).ToString($invariantCulture)
        SM_H100_POLL_INTERVAL_MS = ([int]$Runtime.Config.constants.poll_interval_ms).ToString($invariantCulture)
    }
}

function Invoke-CampaignAttempt {
    param(
        [Parameter(Mandatory = $true)][int]$Slot,
        [Parameter(Mandatory = $true)][int]$Attempt,
        [Parameter(Mandatory = $true)][int]$Seed,
        [Parameter(Mandatory = $true)][string]$RunId,
        [Parameter(Mandatory = $true)][string]$AttemptDirectory,
        [Parameter(Mandatory = $true)][object]$Runtime,
        [Parameter(Mandatory = $true)][Diagnostics.Stopwatch]$CohortWatch
    )

    $exchangeDirectory = Join-Path $AttemptDirectory 'exchange'
    $transcriptDirectory = Join-Path $AttemptDirectory 'transcript'
    New-Item -ItemType Directory -Path $exchangeDirectory, $transcriptDirectory -Force | Out-Null
    $sandbox = New-PlayerSandbox -CohortId $Runtime.CohortId -Slot $Slot -Attempt $Attempt
    $bundle = Get-RunEnvironmentBundle -Seed $Seed -RunId $RunId -OutputPath $AttemptDirectory `
        -ExchangePath $exchangeDirectory -Runtime $Runtime
    Set-EnvironmentBundle -Values $bundle

    $manifestPath = Join-Path $AttemptDirectory 'run-manifest.json'
    $manifest = [ordered]@{
        schema_version = 'H100LiveColdStartRunManifestV1'
        cohort_id = $Runtime.CohortId
        mode = if ($Runtime.DryRun) { 'dry-run-smoke' } else { 'live' }
        slot = $Slot
        harness_attempt = $Attempt
        seed_base = $Seed
        live_run_id = $RunId
        binding = $Runtime.BindingName
        mechanism = if ($Runtime.DryRun) { 'scripted-stub' } else { [string]$Runtime.Binding.mechanism }
        model = if ($Runtime.DryRun) { 'scripted-stub' } else { [string]$Runtime.Binding.model }
        agent_kind = if ($Runtime.DryRun) { 'scripted-stub' } else { [string]$Runtime.Binding.agent_kind }
        player_sandbox_cwd = $sandbox
        player_sandbox_repo_outside = $true
        player_sandbox_empty_at_start = $true
        sandbox = if ($Runtime.DryRun) { 'not-applicable' } else { [string]$Runtime.Binding.sandbox }
        stdin_closed = $true
        unity_method = if ($Runtime.DryRun) {
            'SM.Editor.Validation.H100LiveColdStartCaptureRunner.RunDryRunFromCli'
        }
        else {
            'SM.Editor.Validation.H100LiveColdStartCaptureRunner.RunLiveCaptureFromCli'
        }
        environment = $bundle
        result = [ordered]@{ status = 'pending'; rerun_eligible = $false }
    }
    Write-JsonAtomic -Path $manifestPath -Value $manifest

    $unityHandle = $null
    try {
        $method = [string]$manifest.unity_method
        $relativeUnityLog = Convert-ToProjectRelativePath (Join-Path $AttemptDirectory 'unity-capture.log')
        $unityHandle = Start-UnityMethod -Method $method -LogFile $relativeUnityLog `
            -PhaseName "H100 live cold-start slot $Slot attempt $Attempt" `
            -DriverLogPrefix (Join-Path $AttemptDirectory 'unity-driver')
        $session = [pscustomobject]@{ SessionId = ''; InvocationCount = 0 }
        while (-not $unityHandle.Process.HasExited) {
            if ($CohortWatch.Elapsed.TotalSeconds -ge [int]$Runtime.Config.constants.cohort_watchdog_seconds) {
                throw "Cohort watchdog exceeded $($Runtime.Config.constants.cohort_watchdog_seconds) seconds."
            }
            $null = Service-ExchangeOnce -ExchangeDirectory $exchangeDirectory -TranscriptDirectory $transcriptDirectory `
                -Binding $Runtime.Binding -SessionState $session -SandboxDirectory $sandbox `
                -UseScriptedStub $Runtime.DryRun `
                -DecisionTimeoutSeconds ([int]$Runtime.Config.constants.decision_timeout_seconds) `
                -RunReportTimeoutSeconds ([int]$Runtime.Config.constants.run_report_timeout_seconds)
            if (-not $unityHandle.Process.HasExited) {
                Start-Sleep -Milliseconds ([int]$Runtime.Config.constants.poll_interval_ms)
            }
        }

        $unityResult = Complete-CapturedProcess -Handle $unityHandle
        $unityHandle = $null
        if ($unityResult.ExitCode -ne 0) {
            throw "Unity capture failed with exit code $($unityResult.ExitCode.ToString($invariantCulture))."
        }

        $tracePath = Join-Path $AttemptDirectory 'sealed-decision-trace-v1.json'
        $ledgerPath = Join-Path $AttemptDirectory 'player_visible_fact_ledger.jsonl'
        foreach ($artifactPath in $tracePath, $ledgerPath) {
            if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
                throw "Capture artifact missing: $artifactPath"
            }
        }
        if (@(Get-ChildItem -LiteralPath $exchangeDirectory -Filter '*.tmp' -File -ErrorAction SilentlyContinue).Count -ne 0) {
            throw 'Exchange directory retained temporary files.'
        }
        $trace = Get-Content -Raw -LiteralPath $tracePath | ConvertFrom-Json
        if ([string]$trace.header.prompt_schema_hash -cne $Runtime.PromptSchemaHash) {
            throw "Trace prompt_schema_hash mismatch: $($trace.header.prompt_schema_hash)"
        }
        $expectedSource = if ($Runtime.DryRun) { 0 } else { 1 }
        if ([int]$trace.header.capture_source -ne $expectedSource) {
            throw "Trace capture_source mismatch: expected=$expectedSource actual=$($trace.header.capture_source)"
        }
        if (-not $Runtime.DryRun -and [string]$trace.header.run_id -cne $RunId) {
            throw "Trace run_id mismatch: expected=$RunId actual=$($trace.header.run_id)"
        }

        $taint = Test-TranscriptTaint -TranscriptDirectory $transcriptDirectory
        if ($taint.status -cne 'clean') {
            throw "Transcript taint scan found $($taint.tool_use_events.Count) tool-use event(s)."
        }
        $terminalFailure = @($trace.entries | Where-Object { [bool]$_.terminal_failure }).Count -gt 0
        $manifest.result = [ordered]@{
            status = 'accepted'
            result_kind = if ($terminalFailure) { 'terminal_failure_player_result' } else { 'completed_player_result' }
            rerun_eligible = $false
            terminal_failure = $terminalFailure
            trace_path = $tracePath
            ledger_path = $ledgerPath
            exchange_invocation_count = [int]$session.InvocationCount
            codex_session_id = if ($Runtime.DryRun) { $null } else { [string]$session.SessionId }
            taint_scan = $taint
        }
        Write-JsonAtomic -Path $manifestPath -Value $manifest
        return [pscustomobject]@{
            Accepted = $true
            TerminalFailure = $terminalFailure
            TracePath = $tracePath
            LedgerPath = $ledgerPath
            ExchangeDirectory = $exchangeDirectory
            AttemptDirectory = $AttemptDirectory
            RunManifestPath = $manifestPath
            Environment = $bundle
        }
    }
    catch {
        Stop-CapturedProcess -Handle $unityHandle
        $taint = Test-TranscriptTaint -TranscriptDirectory $transcriptDirectory
        $manifest.result = [ordered]@{
            status = 'disqualified'
            result_kind = 'harness_fault'
            rerun_eligible = $true
            error = $_.Exception.Message
            taint_scan = $taint
        }
        Write-JsonAtomic -Path $manifestPath -Value $manifest
        return [pscustomobject]@{
            Accepted = $false
            Error = $_.Exception.Message
            AttemptDirectory = $AttemptDirectory
            RunManifestPath = $manifestPath
        }
    }
}

function Assert-GateStatus {
    param(
        [Parameter(Mandatory = $true)][string]$ReportPath,
        [Parameter(Mandatory = $true)][string]$GateId
    )

    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
        throw "Gate report missing: $ReportPath"
    }
    $report = Get-Content -Raw -LiteralPath $ReportPath | ConvertFrom-Json
    $matches = @($report.gates | Where-Object { [string]$_.gate_id -ceq $GateId })
    if ($matches.Count -ne 1) {
        throw "Gate report does not contain exactly one $GateId result: $ReportPath"
    }
    return [string]$matches[0].status
}

function Invoke-Bt1AndProvenance {
    param(
        [Parameter(Mandatory = $true)][object]$Run,
        [Parameter(Mandatory = $true)][int]$Slot,
        [Parameter(Mandatory = $true)][object]$Runtime
    )

    $root = Join-Path $Runtime.CohortDirectory "post-gates/slot-$($Slot.ToString('D2', $invariantCulture))/bt1"
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $replayDirectories = @()
    for ($index = 0; $index -lt $hostileCultures.Count; $index++) {
        $number = $index + 1
        $replayDirectory = Join-Path $root "replay-$number"
        $replayDirectories += $replayDirectory
        $bundle = [ordered]@{} + $Run.Environment
        $bundle.SM_H100_TRACE_PATH = $Run.TracePath
        $bundle.SM_H100_REPLAY_RESULT_DIR = $replayDirectory
        $bundle.SM_H100_FORCE_CULTURE = $hostileCultures[$index]
        $bundle.SM_H100_SEALED_OUTPUT = $replayDirectory
        Set-EnvironmentBundle -Values $bundle
        $logFile = Convert-ToProjectRelativePath (Join-Path $root "replay-$number-unity.log")
        $null = Invoke-UnityMethod -Method 'SM.Editor.Validation.H100SealedBridgeRunner.RunReplayFromCli' `
            -LogFile $logFile -PhaseName "H100 BT1 slot $Slot replay $number" `
            -DriverLogPrefix (Join-Path $root "replay-$number-driver")
        foreach ($artifact in 'rebuilt-trace.json', 'replay-env.json') {
            if (-not (Test-Path -LiteralPath (Join-Path $replayDirectory $artifact) -PathType Leaf)) {
                throw "BT1 replay artifact missing: $(Join-Path $replayDirectory $artifact)"
            }
        }
    }

    $gateBundle = [ordered]@{} + $Run.Environment
    $gateBundle.SM_H100_TRACE_PATH = $Run.TracePath
    $gateBundle.SM_H100_REPLAY_RESULT_DIRS = $replayDirectories -join ';'
    $gateBundle.SM_H100_SEALED_OUTPUT = $root
    Set-EnvironmentBundle -Values $gateBundle
    $null = Invoke-UnityMethod -Method 'SM.Editor.Validation.H100SealedBridgeRunner.RunBt1GateFromCli' `
        -LogFile (Convert-ToProjectRelativePath (Join-Path $root 'gate-unity.log')) `
        -PhaseName "H100 BT1 slot $Slot gate" -DriverLogPrefix (Join-Path $root 'gate-driver')
    $gateReportPath = Join-Path $root 'h100-bt1-gate-report.json'
    if ((Assert-GateStatus -ReportPath $gateReportPath -GateId 'BT1') -cne 'pass') {
        throw "BT1 slot $Slot did not pass."
    }
    $witnessPath = Join-Path $root 'bt1-replay-witness.json'
    if (-not (Test-Path -LiteralPath $witnessPath -PathType Leaf)) {
        throw "BT1 witness missing: $witnessPath"
    }
    $witness = Get-Content -Raw -LiteralPath $witnessPath | ConvertFrom-Json
    if ([int]$witness.independent_process_replay_count -lt 3 -or
        [int]$witness.distinct_applied_culture_count -lt 3 -or
        -not [bool]$witness.all_byte_identical) {
        throw "BT1 witness failed slot $Slot replay/culture/byte assertions."
    }

    $provenanceDirectory = Join-Path $root 'provenance-replay'
    $provenanceBundle = [ordered]@{} + $Run.Environment
    $provenanceBundle.SM_H100_TRACE_PATH = $Run.TracePath
    $provenanceBundle.SM_H100_REPLAY_RESULT_DIR = $provenanceDirectory
    $provenanceBundle.SM_H100_SEALED_OUTPUT = $provenanceDirectory
    $provenanceBundle.SM_H100_PROMPT_ARCHIVE_DIR = $Run.ExchangeDirectory
    Set-EnvironmentBundle -Values $provenanceBundle
    $null = Invoke-UnityMethod -Method 'SM.Editor.Validation.H100SealedBridgeRunner.RunReplayFromCli' `
        -LogFile (Convert-ToProjectRelativePath (Join-Path $root 'provenance-unity.log')) `
        -PhaseName "H100 slot $Slot prompt provenance replay" `
        -DriverLogPrefix (Join-Path $root 'provenance-driver')
    foreach ($artifact in 'rebuilt-trace.json', 'replay-env.json') {
        if (-not (Test-Path -LiteralPath (Join-Path $provenanceDirectory $artifact) -PathType Leaf)) {
            throw "Provenance replay artifact missing: $(Join-Path $provenanceDirectory $artifact)"
        }
    }

    $manifestHashes = @($witness.per_replay |
        ForEach-Object { [string]$_.fingerprint.sealed_manifest_hash } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique)
    if ($manifestHashes.Count -ne 1) {
        throw "BT1 witness did not bind exactly one sealed manifest hash: $witnessPath"
    }
    return [pscustomobject]@{
        GateReportPath = $gateReportPath
        WitnessPath = $witnessPath
        TraceManifestHash = [string]$manifestHashes[0]
        ProvenanceReplayDirectory = $provenanceDirectory
    }
}

function Invoke-CohortScorers {
    param(
        [Parameter(Mandatory = $true)][object[]]$Runs,
        [Parameter(Mandatory = $true)][object[]]$Bt1Results,
        [Parameter(Mandatory = $true)][object]$Runtime
    )

    $postRoot = Join-Path $Runtime.CohortDirectory 'post-gates'
    $tracePaths = @($Runs | ForEach-Object { $_.TracePath })
    $ledgerPaths = @($Runs | ForEach-Object { $_.LedgerPath })
    $hashes = @($Bt1Results | ForEach-Object { $_.TraceManifestHash })
    $ownerDraftPath = Join-Path $postRoot 'owner-approval-draft.json'
    Write-JsonAtomic -Path $ownerDraftPath -Immutable -Value ([ordered]@{
        approved = $false
        statement = 'DRAFT ONLY: owner witness review has not approved this cohort.'
        approved_on = ''
        bound_trace_manifest_hashes = $hashes
    })

    $common = [ordered]@{
        SM_H100_TRACE_PATHS = $tracePaths -join ';'
        SM_H100_LEDGER_PATHS = $ledgerPaths -join ';'
        SM_H100_EXPECTED_PROMPT_SCHEMA_HASH = $Runtime.PromptSchemaHash
    }

    $bt5Directory = Join-Path $postRoot 'bt5'
    $bt5Bundle = [ordered]@{} + $common
    $bt5Bundle.SM_H100_SEALED_OUTPUT = $bt5Directory
    Set-EnvironmentBundle -Values $bt5Bundle
    $bt5Process = Invoke-UnityMethod -Method 'SM.Editor.Validation.H100Bt5Bt10GateRunner.RunBt5GateFromCli' `
        -LogFile (Convert-ToProjectRelativePath (Join-Path $bt5Directory 'unity.log')) `
        -PhaseName 'H100 BT5 cohort gate' -DriverLogPrefix (Join-Path $bt5Directory 'driver') -AllowFailure
    $bt5Report = Join-Path $bt5Directory 'h100-bt1-gate-report.json'
    $bt5Status = Assert-GateStatus -ReportPath $bt5Report -GateId 'BT5'
    if ($bt5Status -ceq 'pass' -and $bt5Process.ExitCode -ne 0) {
        throw 'BT5 process failed despite a passing gate report.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $bt5Directory 'bt5-witness.json') -PathType Leaf)) {
        throw 'BT5 witness is missing.'
    }

    $approvalForGate = if ([string]::IsNullOrWhiteSpace($OwnerApprovalPath)) {
        $ownerDraftPath
    }
    else {
        Resolve-ProjectPath -Path $OwnerApprovalPath -MustExist
    }
    $bt10Directory = Join-Path $postRoot 'bt10'
    $bt10Bundle = [ordered]@{} + $common
    $bt10Bundle.SM_H100_SEALED_OUTPUT = $bt10Directory
    $bt10Bundle.SM_H100_OWNER_APPROVAL_PATH = $approvalForGate
    Set-EnvironmentBundle -Values $bt10Bundle
    $bt10Process = Invoke-UnityMethod -Method 'SM.Editor.Validation.H100Bt5Bt10GateRunner.RunBt10GateFromCli' `
        -LogFile (Convert-ToProjectRelativePath (Join-Path $bt10Directory 'unity.log')) `
        -PhaseName 'H100 BT10 cohort gate' -DriverLogPrefix (Join-Path $bt10Directory 'driver') -AllowFailure
    $bt10Report = Join-Path $bt10Directory 'h100-bt1-gate-report.json'
    $bt10Status = Assert-GateStatus -ReportPath $bt10Report -GateId 'BT10'
    if ($bt10Status -ceq 'pass' -and $bt10Process.ExitCode -ne 0) {
        throw 'BT10 process failed despite a passing gate report.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $bt10Directory 'bt10-witness.json') -PathType Leaf)) {
        throw 'BT10 witness is missing.'
    }

    if ($Runtime.DryRun) {
        if ($bt5Status -cne 'fail' -or $bt10Status -cne 'fail') {
            throw "Synthetic dry-run must be rejected by BT5 and BT10 (BT5=$bt5Status BT10=$bt10Status)."
        }
        $bt5Witness = Get-Content -Raw -LiteralPath (Join-Path $bt5Directory 'bt5-witness.json') | ConvertFrom-Json
        $bt10Witness = Get-Content -Raw -LiteralPath (Join-Path $bt10Directory 'bt10-witness.json') | ConvertFrom-Json
        if ([int]$bt5Witness.valid_run_count -ne 0 -or [int]$bt10Witness.valid_run_count -ne 0) {
            throw 'Synthetic dry-run unexpectedly entered the valid scorer cohort.'
        }
    }

    $pipelinePath = Join-Path $postRoot 'post-pipeline-manifest.json'
    Write-JsonAtomic -Path $pipelinePath -Value ([ordered]@{
        schema_version = 'H100LiveColdStartPostPipelineV1'
        cohort_id = $Runtime.CohortId
        run_count = $Runs.Count
        bt1 = @($Bt1Results | ForEach-Object {
            [ordered]@{
                gate_status = 'pass'
                gate_report = $_.GateReportPath
                witness = $_.WitnessPath
                prompt_provenance_replay = $_.ProvenanceReplayDirectory
                trace_manifest_hash = $_.TraceManifestHash
            }
        })
        bt5 = [ordered]@{ status = $bt5Status; process_exit_code = $bt5Process.ExitCode; report = $bt5Report }
        bt10 = [ordered]@{
            status = $bt10Status
            process_exit_code = $bt10Process.ExitCode
            report = $bt10Report
            approval_input = $approvalForGate
            owner_approval_pending = [string]::IsNullOrWhiteSpace($OwnerApprovalPath)
        }
        owner_approval_draft = $ownerDraftPath
        synthetic_rejection_expected = $Runtime.DryRun
    })
    return [pscustomobject]@{
        ManifestPath = $pipelinePath
        Bt5Status = $bt5Status
        Bt10Status = $bt10Status
        OwnerApprovalDraftPath = $ownerDraftPath
    }
}

function Assert-Config {
    param([Parameter(Mandatory = $true)][object]$Config)

    if ([string]$Config.schema_version -cne 'H100LiveColdStartDriverConfigV1') {
        throw "Unsupported config schema: $($Config.schema_version)"
    }
    if ([string]$Config.cohort_id -notmatch '^[a-z0-9][a-z0-9._-]+$') {
        throw "cohort_id must be filesystem-safe lowercase text: $($Config.cohort_id)"
    }
    $expectedSeeds = 1701..1706
    $actualSeeds = @($Config.campaign.seeds | ForEach-Object { [int]$_ })
    if ($actualSeeds.Count -ne 6 -or (Compare-Object $expectedSeeds $actualSeeds)) {
        throw "campaign.seeds must equal 1701..1706 in order."
    }
    $expectedConstants = [ordered]@{
        decision_timeout_seconds = 900
        run_report_timeout_seconds = 1200
        attempt_limit = 3
        harness_rerun_cap_per_slot = 2
        cohort_watchdog_seconds = 43200
        poll_interval_ms = 500
    }
    foreach ($entry in $expectedConstants.GetEnumerator()) {
        if ([int]$Config.constants.($entry.Key) -ne $entry.Value) {
            throw "constants.$($entry.Key) must equal $($entry.Value)."
        }
    }
    $bindingName = [string]$Config.active_binding
    $bindingProperty = $Config.bindings.PSObject.Properties[$bindingName]
    if ($null -eq $bindingProperty) {
        throw "active_binding '$bindingName' is not defined."
    }
    $binding = $bindingProperty.Value
    if ([string]$binding.mechanism -cne 'codex-exec' -or
        [string]$binding.agent_kind -cne 'codex-exec' -or
        [string]$binding.sandbox -cne 'read-only') {
        throw 'The codex binding must use mechanism=codex-exec, agent_kind=codex-exec, sandbox=read-only.'
    }
    if ([string]::IsNullOrWhiteSpace([string]$binding.model)) {
        throw 'The codex binding model is required.'
    }
    return $binding
}

$environmentSnapshot = Save-Environment
try {
    Assert-UnityEditorClosed
    $resolvedConfigPath = Resolve-ProjectPath -Path $ConfigPath -MustExist
    $config = Get-Content -Raw -LiteralPath $resolvedConfigPath | ConvertFrom-Json
    $binding = Assert-Config -Config $config
    $promptTemplatePath = Resolve-ProjectPath -Path ([string]$config.prompt_manifest.prompt_template_file) -MustExist
    $coldStartBriefingPath = Resolve-ProjectPath -Path ([string]$config.prompt_manifest.cold_start_briefing_file) -MustExist
    $promptTemplate = [IO.File]::ReadAllText($promptTemplatePath, $utf8WithoutBom)
    $coldStartBriefing = [IO.File]::ReadAllText($coldStartBriefingPath, $utf8WithoutBom)
    $codexVersionOutput = (& $codexPath --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($codexVersionOutput)) {
        throw 'codex --version failed while freezing the cohort manifest.'
    }
    $codexVersionToken = ($codexVersionOutput -replace '\s+', '-').ToLowerInvariant()
    $modelSnapshotId = "$($binding.mechanism)/$($binding.model)@$codexVersionToken"
    $decodingConfig = "mechanism=$($binding.mechanism);model=$($binding.model);reasoning_effort=$($binding.reasoning_effort);sandbox=$($binding.sandbox)"
    $promptSchemaHash = Get-PromptSchemaHash -PromptTemplateId ([string]$config.prompt_manifest.prompt_template_id) `
        -PromptTemplate $promptTemplate -ColdStartBriefing $coldStartBriefing `
        -ModelSnapshotId $modelSnapshotId -DecodingConfig $decodingConfig

    $cohortId = if ($DryRunSmoke) { "$($config.cohort_id)-dryrun" } elseif ($LiveSmoke) { "$($config.cohort_id)-livesmoke" } else { [string]$config.cohort_id }
    $outputRoot = Resolve-ProjectPath -Path $OutputDirectory
    $cohortDirectory = Join-Path $outputRoot $cohortId
    if (Test-Path -LiteralPath $cohortDirectory) {
        throw "Cohort directory already exists; refusing to overwrite evidence: $cohortDirectory"
    }
    New-Item -ItemType Directory -Path $cohortDirectory -Force | Out-Null
    $seeds = @($config.campaign.seeds | ForEach-Object { [int]$_ })
    $slotMap = for ($index = 0; $index -lt $seeds.Count; $index++) {
        $slot = $index + 1
        [ordered]@{
            slot = $slot
            seed_base = $seeds[$index]
            live_run_id = "$cohortId-slot-$($slot.ToString('D2', $invariantCulture))"
        }
    }
    $executionSlotMap = if ($DryRunSmoke -or $LiveSmoke) { @($slotMap[0]) } else { @($slotMap) }
    $cohortManifestPath = Join-Path $cohortDirectory 'cohort-manifest.json'
    Write-JsonAtomic -Path $cohortManifestPath -Immutable -Value ([ordered]@{
        schema_version = 'H100LiveColdStartCohortManifestV1'
        cohort_id = $cohortId
        mode = if ($DryRunSmoke) { 'dry-run-smoke' } elseif ($LiveSmoke) { 'live-smoke' } else { 'live' }
        prompt_template_id = [string]$config.prompt_manifest.prompt_template_id
        prompt_template_file = Convert-ToProjectRelativePath $promptTemplatePath
        prompt_template_sha256 = Get-FileSha256 $promptTemplatePath
        prompt_template = $promptTemplate
        cold_start_briefing_file = Convert-ToProjectRelativePath $coldStartBriefingPath
        cold_start_briefing_sha256 = Get-FileSha256 $coldStartBriefingPath
        cold_start_briefing = $coldStartBriefing
        model_snapshot_id = $modelSnapshotId
        decoding_config_canonical = $decodingConfig
        prompt_schema_hash = $promptSchemaHash
        binding = [ordered]@{
            name = [string]$config.active_binding
            mechanism = [string]$binding.mechanism
            model = [string]$binding.model
            sandbox = [string]$binding.sandbox
            reasoning_effort = [string]$binding.reasoning_effort
            agent_kind = [string]$binding.agent_kind
        }
        seeds = $seeds
        slot_run_ids = $slotMap
        constants = $config.constants
    })

    $runtime = [pscustomobject]@{
        Config = $config
        Binding = $binding
        BindingName = [string]$config.active_binding
        PromptTemplatePath = $promptTemplatePath
        ColdStartBriefingPath = $coldStartBriefingPath
        ModelSnapshotId = $modelSnapshotId
        DecodingConfig = $decodingConfig
        PromptSchemaHash = $promptSchemaHash
        CohortId = $cohortId
        CohortDirectory = $cohortDirectory
        DryRun = $DryRunSmoke.IsPresent
    }

    $cohortWatch = [Diagnostics.Stopwatch]::StartNew()
    $acceptedRuns = @()
    foreach ($slotEntry in $executionSlotMap) {
        $accepted = $null
        $maxHarnessAttempts = 1 + [int]$config.constants.harness_rerun_cap_per_slot
        for ($attempt = 1; $attempt -le $maxHarnessAttempts; $attempt++) {
            $slotText = ([int]$slotEntry.slot).ToString('D2', $invariantCulture)
            $attemptDirectory = Join-Path $cohortDirectory (
                "slot-$slotText-attempt-$($attempt.ToString('D2', $invariantCulture))")
            New-Item -ItemType Directory -Path $attemptDirectory -Force | Out-Null
            $result = Invoke-CampaignAttempt -Slot ([int]$slotEntry.slot) -Attempt $attempt `
                -Seed ([int]$slotEntry.seed_base) -RunId ([string]$slotEntry.live_run_id) `
                -AttemptDirectory $attemptDirectory -Runtime $runtime -CohortWatch $cohortWatch
            if ($result.Accepted) {
                $accepted = $result
                break
            }
            Write-Warning "Harness fault in slot $($slotEntry.slot) attempt $($attempt): $($result.Error)"
        }
        if ($null -eq $accepted) {
            throw "Slot $($slotEntry.slot) exhausted the harness re-run cap; cohort aborted."
        }
        $acceptedRuns += $accepted
    }

    $bt1Results = @()
    for ($index = 0; $index -lt $acceptedRuns.Count; $index++) {
        $bt1Results += Invoke-Bt1AndProvenance -Run $acceptedRuns[$index] -Slot ($index + 1) -Runtime $runtime
    }
    $post = if ($LiveSmoke) {
        # 1-campaign live smoke: BT1 (replay determinism on the single live trace) runs above; the
        # BT5/BT10 cohort scorers require the full 6-run cohort, so they are intentionally skipped here.
        [pscustomobject]@{
            ManifestPath = ''
            Bt5Status = 'skipped-live-smoke'
            Bt10Status = 'skipped-live-smoke'
            OwnerApprovalDraftPath = ''
        }
    }
    else {
        Invoke-CohortScorers -Runs $acceptedRuns -Bt1Results $bt1Results -Runtime $runtime
    }
    Write-JsonAtomic -Path (Join-Path $cohortDirectory 'cohort-run-manifest.json') -Value ([ordered]@{
        schema_version = 'H100LiveColdStartCohortRunV1'
        cohort_manifest = $cohortManifestPath
        accepted_run_count = $acceptedRuns.Count
        accepted_runs = @($acceptedRuns | ForEach-Object {
            [ordered]@{
                attempt_directory = $_.AttemptDirectory
                trace_path = $_.TracePath
                ledger_path = $_.LedgerPath
                run_manifest = $_.RunManifestPath
                terminal_failure = $_.TerminalFailure
            }
        })
        post_pipeline_manifest = $post.ManifestPath
        bt5_status = $post.Bt5Status
        bt10_status = $post.Bt10Status
        owner_approval_draft = $post.OwnerApprovalDraftPath
    })

    Write-Host "H100 live cold-start driver complete: mode=$(if ($DryRunSmoke) { 'dry-run-smoke' } else { 'live' }) runs=$($acceptedRuns.Count) BT5=$($post.Bt5Status) BT10=$($post.Bt10Status)"
    Write-Host "Cohort evidence: $cohortDirectory"
}
finally {
    Restore-Environment -Snapshot $environmentSnapshot
}

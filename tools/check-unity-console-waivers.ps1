param(
    [string]$RepoRoot = ".",
    [int]$Lines = 120,
    [string]$Filter = "warning,error",
    [switch]$FromStdIn
)

$ErrorActionPreference = "Stop"

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$unityBridge = Join-Path $repo "tools/unity-bridge.ps1"

if ($FromStdIn) {
    $consoleText = [Console]::In.ReadToEnd()
    $exitCode = 0
}
else {
    $output = & pwsh -File $unityBridge console -Lines $Lines -Filter $Filter 2>&1
    $exitCode = $LASTEXITCODE
    $consoleText = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
}

if ($exitCode -ne 0) {
    Write-Error "unity console command failed with exit code $exitCode.`n$consoleText"
    exit $exitCode
}

function ConvertTo-ConsoleEntries {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $trimmed = $Text.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        return @()
    }

    if ($trimmed.StartsWith("[") -and $trimmed.EndsWith("]")) {
        try {
            $parsed = $trimmed | ConvertFrom-Json
            if ($null -eq $parsed) {
                return @()
            }

            return @($parsed | ForEach-Object { $_.ToString() })
        }
        catch {
            # Fall back to line scanning below when Unity returns partial text.
        }
    }

    return @($Text -split "`r?`n")
}

$waivers = @(
    '^\s*$',
    '^\s*\[\]\s*$',
    '(?i)no\s+(console\s+)?(log\s+)?entries',
    '(?i)no\s+entries\s+found',
    "Assembly for Assembly Definition File '.+SM\.Tests\.EditMode\.Integration\.asmdef' will not be compiled, because it has no scripts associated with it\.",
    '(?i)Shader Hidden/ChartRasterizerHardware is not supported',
    '(?i)Access token is unavailable; failed to update',
    '(?i)LicensingClient has failed validation; ignoring',
    '(?i)Assembly .+ has duplicate hint path\. Ignoring ',
    'The type Unity\.Localization UnityEngine\.Localization\.SmartFormat\.Extensions\.ListFormatter is being serialized by \[SerializeReference\], but its parent type Unity\.Localization UnityEngine\.Localization\.SmartFormat\.Core\.Extensions\.FormatterBase is missing the \[Serializable\] attribute\.',
    'The type Unity\.Localization UnityEngine\.Localization\.Metadata\.SmartFormatTag is being serialized by \[SerializeReference\], but its parent type Unity\.Localization UnityEngine\.Localization\.Metadata\.SharedTableEntryMetadata is missing the \[Serializable\] attribute\.',
    '(?s)^The referenced script on this Behaviour \(Game Object ''<null>''\) is missing!\s+UnityEngine\.Resources:Load<SM\.Unity\.BattleVfxCatalog>.*SM\.Unity\.UI\.Town\.Preview\.CompendiumVfxPreviewResolver:ResolveCatalog',
    '^Releasing render texture that is set as Camera\.targetTexture!$'
)

$unknown = New-Object System.Collections.Generic.List[string]
$waived = 0

foreach ($line in (ConvertTo-ConsoleEntries -Text $consoleText)) {
    $matched = $false
    foreach ($pattern in $waivers) {
        if ($line -match $pattern) {
            $matched = $true
            break
        }
    }

    if ($matched) {
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            $waived++
        }
        continue
    }

    $unknown.Add($line)
}

if ($unknown.Count -gt 0) {
    Write-Host "Unity console waiver check failed: unknown=$($unknown.Count), waived=$waived"
    $unknown | Select-Object -First 40 | ForEach-Object { Write-Host "UNKNOWN: $_" }
    exit 1
}

Write-Host "Unity console waiver check passed: unknown=0, waived=$waived"

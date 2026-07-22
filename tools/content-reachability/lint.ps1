param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$catalogPath = Join-Path $PSScriptRoot 'field-catalog.tsv'
$fallbackPath = Join-Path $PSScriptRoot 'fallback-registry.tsv'
$definitionRoot = Join-Path $RepoRoot 'Assets/Resources/_Game/Content/Definitions'
$failed = $false

function Write-ReachabilityError {
    param([string]$Detail)
    Write-Host "  ERROR: $Detail" -ForegroundColor Red
    $script:failed = $true
}

function Get-FilePart {
    param([string]$FileLine)
    if ($FileLine -notmatch '^(?<path>.+):(?<line>\d+)$') {
        return $null
    }

    return $Matches['path']
}

function Get-TypeBodyFields {
    param(
        [string]$FullTypeName,
        [string]$RelativePath
    )

    $path = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        Write-ReachabilityError "Audited definition source is missing: $RelativePath. Wrong: the field catalog no longer maps to source. Runtime actually has no proven authored-to-runtime contract. Choose wire it / delete it / mark it, then update the catalog."
        return @()
    }

    $simpleName = $FullTypeName.Substring($FullTypeName.LastIndexOf('.') + 1)
    $lines = @(Get-Content -LiteralPath $path)
    $declarationIndex = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match "\b(?:class|struct)\s+$([regex]::Escape($simpleName))\b") {
            $declarationIndex = $index
            break
        }
    }

    if ($declarationIndex -lt 0) {
        Write-ReachabilityError "Audited type '$FullTypeName' is missing from $RelativePath. Wrong: the field catalog points at a type that source no longer declares. Runtime actually has no proven consumer contract. Choose wire it / delete it / mark it, then update the catalog."
        return @()
    }

    $foundOpen = $false
    $depth = 0
    $inBlockComment = $false
    $attributeBuffer = ''
    $result = [System.Collections.Generic.List[object]]::new()

    for ($index = $declarationIndex; $index -lt $lines.Count; $index++) {
        $original = $lines[$index]
        $masked = $original

        if ($inBlockComment) {
            $end = $masked.IndexOf('*/', [StringComparison]::Ordinal)
            if ($end -lt 0) { continue }
            $masked = $masked.Substring($end + 2)
            $inBlockComment = $false
        }

        while ($masked -match '/\*') {
            $start = $masked.IndexOf('/*', [StringComparison]::Ordinal)
            $end = $masked.IndexOf('*/', $start + 2, [StringComparison]::Ordinal)
            if ($end -lt 0) {
                $masked = $masked.Substring(0, $start)
                $inBlockComment = $true
                break
            }
            $masked = $masked.Remove($start, $end + 2 - $start)
        }

        $masked = [regex]::Replace($masked, '"(?:\\.|[^"\\])*"', '""')
        $comment = $masked.IndexOf('//', [StringComparison]::Ordinal)
        if ($comment -ge 0) { $masked = $masked.Substring(0, $comment) }

        if (-not $foundOpen) {
            $openIndex = $masked.IndexOf('{')
            if ($openIndex -lt 0) { continue }
            $foundOpen = $true
        }

        if ($foundOpen -and $depth -eq 1) {
            $trimmed = $masked.Trim()
            if ($trimmed.StartsWith('[', [StringComparison]::Ordinal)) {
                $attributeBuffer = "$attributeBuffer $trimmed"
            }

            $withoutInlineAttributes = [regex]::Replace($trimmed, '^(?:\[[^\]]+\]\s*)+', '')
            $fieldMatch = [regex]::Match(
                $withoutInlineAttributes,
                '^(?<access>public|private|internal|protected)\s+(?<mods>(?:(?:static|readonly|const|volatile|new)\s+)*)[^=;(){}]+?\s+(?<name>[A-Za-z_]\w*)\s*(?:=(?!>)|;)')
            if ($fieldMatch.Success) {
                $access = $fieldMatch.Groups['access'].Value
                $modifiers = $fieldMatch.Groups['mods'].Value
                $serializedPrivate = "$attributeBuffer $trimmed" -match '\bSerializeField\b'
                if ($modifiers -notmatch '\b(?:static|readonly|const)\b' -and ($access -eq 'public' -or $serializedPrivate)) {
                    $result.Add([pscustomobject]@{
                        Field = "$FullTypeName.$($fieldMatch.Groups['name'].Value)"
                        FileLine = "$RelativePath`:$($index + 1)"
                    })
                }
                $attributeBuffer = ''
            }
            elseif ($trimmed.Length -gt 0 -and -not $trimmed.StartsWith('[', [StringComparison]::Ordinal)) {
                $attributeBuffer = ''
            }
        }

        $openCount = ([regex]::Matches($masked, '\{')).Count
        $closeCount = ([regex]::Matches($masked, '\}')).Count
        $depth += $openCount - $closeCount
        if ($foundOpen -and $depth -le 0) { break }
    }

    return @($result)
}

function Test-TrapValue {
    param(
        [string]$Rule,
        [string]$RawValue
    )

    $value = $RawValue.Trim()
    if ($Rule -eq 'empty') { return [string]::IsNullOrWhiteSpace($value) -or $value -eq '[]' -or $value -eq '{fileID: 0}' }
    if ($Rule -eq 'nonempty') { return -not [string]::IsNullOrWhiteSpace($value) -and $value -ne '[]' -and $value -ne '{fileID: 0}' }

    $number = 0.0
    if (-not [double]::TryParse($value, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) {
        return $false
    }

    if ($Rule -match '^eq:(?<n>-?[0-9.]+)$') { return $number -eq [double]::Parse($Matches['n'], [Globalization.CultureInfo]::InvariantCulture) }
    if ($Rule -match '^lt:(?<n>-?[0-9.]+)$') { return $number -lt [double]::Parse($Matches['n'], [Globalization.CultureInfo]::InvariantCulture) }
    if ($Rule -match '^lte:(?<n>-?[0-9.]+)$') { return $number -le [double]::Parse($Matches['n'], [Globalization.CultureInfo]::InvariantCulture) }
    if ($Rule -match '^gt:(?<n>-?[0-9.]+)$') { return $number -gt [double]::Parse($Matches['n'], [Globalization.CultureInfo]::InvariantCulture) }
    if ($Rule -match '^outside:(?<lo>-?[0-9.]+):(?<hi>-?[0-9.]+)$') {
        $lo = [double]::Parse($Matches['lo'], [Globalization.CultureInfo]::InvariantCulture)
        $hi = [double]::Parse($Matches['hi'], [Globalization.CultureInfo]::InvariantCulture)
        return $number -lt $lo -or $number -gt $hi
    }

    return $false
}

if (-not (Test-Path -LiteralPath $catalogPath)) {
    Write-ReachabilityError "Missing reachability catalog: $catalogPath. Wrong: authored fields can be added without consumer evidence. Runtime actually ignores an unconsumed field. Choose wire it / delete it / mark it before adding it."
}
if (-not (Test-Path -LiteralPath $fallbackPath)) {
    Write-ReachabilityError "Missing fallback registry: $fallbackPath. Wrong: authored sentinels can silently change meaning. Runtime actually applies the unreported fallback. Choose wire it / delete it / mark it before adding it."
}
if ($failed) { exit 1 }

$catalog = @(Import-Csv -LiteralPath $catalogPath -Delimiter "`t")
$fallbacks = @(Import-Csv -LiteralPath $fallbackPath -Delimiter "`t")
$allowedClassifications = @('unwired', 'shadowed', 'dead', 'presentation-only', 'live')
$requiredColumns = @('classification', 'field', 'definition_file_line', 'consumer_file_line', 'runtime_effect_or_winner', 'recommended_disposition', 'marker')
$requiredFallbackColumns = @('classification', 'file_line', 'authored_field', 'sentinel', 'falls_back_to', 'author_can_tell', 'guard_rule', 'asset_glob', 'guard_message')

foreach ($column in $requiredColumns) {
    if ($catalog.Count -eq 0 -or $catalog[0].PSObject.Properties.Name -notcontains $column) {
        Write-ReachabilityError "field-catalog.tsv is missing column '$column'. Wrong: consumer evidence cannot be enforced. Runtime actually has no proven effect for unregistered data. Choose wire it / delete it / mark it, then restore the schema."
    }
}
foreach ($column in $requiredFallbackColumns) {
    if ($fallbacks.Count -eq 0 -or $fallbacks[0].PSObject.Properties.Name -notcontains $column) {
        Write-ReachabilityError "fallback-registry.tsv is missing column '$column'. Wrong: fallback behavior cannot be enforced. Runtime actually applies an unreported sentinel or clamp. Choose wire it / delete it / mark it, then restore the registry schema."
    }
}

$fallbackDuplicates = @($fallbacks | Group-Object { "$($_.file_line)|$($_.authored_field)|$($_.sentinel)" } | Where-Object Count -gt 1)
foreach ($duplicate in $fallbackDuplicates) {
    Write-ReachabilityError "Duplicate fallback registry row '$($duplicate.Name)'. Wrong: duplicate dispositions obscure the runtime behavior. Runtime actually follows one executable fallback. Choose wire it / delete it / mark it, then leave one evidence row."
}

foreach ($fallback in $fallbacks) {
    $fallbackPathPart = Get-FilePart $fallback.file_line
    if ([string]::IsNullOrWhiteSpace($fallbackPathPart) -or -not (Test-Path -LiteralPath (Join-Path $RepoRoot $fallbackPathPart))) {
        Write-ReachabilityError "Fallback source evidence is missing for '$($fallback.authored_field)': '$($fallback.file_line)'. Wrong: the registered sentinel cannot be located. Runtime actually follows only executable code. Choose wire it / delete it / mark it, then repair the evidence."
    }
    elseif ($fallback.file_line -match ':(?<line>\d+)$') {
        $sourceLineCount = @(Get-Content -LiteralPath (Join-Path $RepoRoot $fallbackPathPart)).Count
        if ([int]$Matches['line'] -gt $sourceLineCount) {
            Write-ReachabilityError "Fallback source line is outside '$fallbackPathPart' for '$($fallback.authored_field)': '$($fallback.file_line)'. Wrong: stale evidence hides the real sentinel. Runtime actually follows the current source. Choose wire it / delete it / mark it, then repair the evidence."
        }
    }

    if ($fallback.author_can_tell -notin @('true', 'false')) {
        Write-ReachabilityError "Fallback '$($fallback.authored_field)' has invalid author_can_tell '$($fallback.author_can_tell)'. Wrong: author visibility is unknown. Runtime actually follows '$($fallback.falls_back_to)'. Choose wire it / delete it / mark it, then record true or false."
    }

    if ($fallback.classification -eq 'legitimate-sentinel' -and $fallback.guard_rule -ne 'documented') {
        Write-ReachabilityError "Legitimate sentinel '$($fallback.authored_field)' is not marked documented. Wrong: its fallback contract is not durable. Runtime actually follows '$($fallback.falls_back_to)'. Choose wire it / delete it / mark it, then document the sentinel."
    }
    elseif ($fallback.classification -eq 'trap' -and $fallback.guard_rule -notmatch '^(?:missing|empty|nonempty|eq:-?[0-9.]+|lt:-?[0-9.]+|lte:-?[0-9.]+|gt:-?[0-9.]+|outside:-?[0-9.]+:-?[0-9.]+|existing-validator:.+)$') {
        Write-ReachabilityError "Trap '$($fallback.authored_field)' has unsupported guard_rule '$($fallback.guard_rule)'. Wrong: authoring the sentinel would produce no warning. Runtime actually follows '$($fallback.falls_back_to)'. Choose wire it / delete it / mark it, then add a supported guard."
    }
}

$duplicates = @($catalog | Group-Object field | Where-Object Count -gt 1)
foreach ($duplicate in $duplicates) {
    Write-ReachabilityError "Duplicate catalog field '$($duplicate.Name)'. Wrong: two dispositions obscure the runtime winner. Runtime actually follows only executable code. Choose wire it / delete it / mark it, then leave one evidence row."
}

$rank = @{ 'unwired' = 0; 'shadowed' = 1; 'dead' = 2; 'presentation-only' = 3; 'live' = 4 }
$lastRank = -1
foreach ($row in $catalog) {
    if ($allowedClassifications -notcontains $row.classification) {
        Write-ReachabilityError "Unknown classification '$($row.classification)' for '$($row.field)'. Wrong: the authored field has no enforceable disposition. Runtime actually follows only a named consumer. Choose wire it / delete it / mark it, then classify it."
        continue
    }
    $currentRank = $rank[$row.classification]
    if ($currentRank -lt $lastRank) {
        Write-ReachabilityError "Catalog ordering is invalid at '$($row.field)'. Unwired and shadowed fields must stay first so the latent mechanics are visible."
    }
    $lastRank = $currentRank

    $definitionPath = Get-FilePart $row.definition_file_line
    if ([string]::IsNullOrWhiteSpace($definitionPath) -or -not (Test-Path -LiteralPath (Join-Path $RepoRoot $definitionPath))) {
        Write-ReachabilityError "Definition evidence is missing for '$($row.field)': '$($row.definition_file_line)'. Wrong: the authored surface cannot be located. Runtime actually has no proven consumer. Choose wire it / delete it / mark it, then repair the evidence."
    }

    if ($row.classification -in @('live', 'presentation-only', 'shadowed')) {
        $consumerPath = Get-FilePart $row.consumer_file_line
        if ([string]::IsNullOrWhiteSpace($consumerPath) -or -not (Test-Path -LiteralPath (Join-Path $RepoRoot $consumerPath))) {
            Write-ReachabilityError "Consumer evidence is missing for '$($row.field)': '$($row.consumer_file_line)'. Wrong: an author can set the field but no executable consumer is proven. Runtime actually does nothing until a battle/meta/persistence/presentation read exists. Choose wire it / delete it / mark it, then update the evidence."
        }
        elseif ($consumerPath -match '/(?:Editor|Tests|ContentParsing|HeadlessMetrics|HeadlessCensus|HeadlessPolicies)/') {
            Write-ReachabilityError "Non-runtime evidence '$consumerPath' is registered for '$($row.field)'. Wrong: inspectors, validators, parsers, tests, and fact projectors are not consumers. Runtime actually does nothing. Choose wire it / delete it / mark it, then name an executable consumer."
        }
    }
    elseif ($row.consumer_file_line -ne '-') {
        Write-ReachabilityError "'$($row.field)' is '$($row.classification)' but claims consumer '$($row.consumer_file_line)'. Wrong: a non-consumer marker cannot masquerade as runtime behavior. Runtime actually does nothing. Choose wire it / delete it / mark it and keep consumer evidence '-'."
    }

    if ($row.classification -eq 'dead' -and $row.marker -ne 'reachability-catalog+lint-warning') {
        Write-ReachabilityError "Dead field '$($row.field)' lacks the enforceable marker. Wrong: authors can keep writing a value that runtime ignores. Runtime actually does nothing. Choose wire it / delete it / mark it; the accepted marker is 'reachability-catalog+lint-warning'."
    }
}

$unwiredRows = @($catalog | Where-Object classification -eq 'unwired')
$shadowedRows = @($catalog | Where-Object classification -eq 'shadowed')
if ($unwiredRows.Count -gt 0 -or $shadowedRows.Count -gt 0) {
    $sample = @($unwiredRows + $shadowedRows | Select-Object -First 8 | ForEach-Object field) -join ', '
    Write-Warning "[content reachability latent mechanics] unwired=$($unwiredRows.Count), shadowed=$($shadowedRows.Count) (sample: $sample). Wrong: authored values can look live without a named consumer or can lose to a compiler winner. Runtime actually ignores unwired values and uses the cataloged winner for shadowed values. Choose wire it / delete it / mark it; behavior changes require a separately ratified unit."
}

$typeSource = @{}
foreach ($row in $catalog) {
    $lastDot = $row.field.LastIndexOf('.')
    $typeName = $row.field.Substring(0, $lastDot)
    $sourcePath = Get-FilePart $row.definition_file_line
    if (-not $typeSource.ContainsKey($typeName)) { $typeSource[$typeName] = $sourcePath }
    elseif ($typeSource[$typeName] -ne $sourcePath) {
        Write-ReachabilityError "Type '$typeName' maps to multiple source files. Wrong: source discovery cannot prove the authored surface. Runtime actually follows only compiled fields. Choose wire it / delete it / mark it, then repair the catalog."
    }
}

$discovered = [System.Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($entry in $typeSource.GetEnumerator()) {
    foreach ($field in Get-TypeBodyFields -FullTypeName $entry.Key -RelativePath $entry.Value) {
        $discovered[$field.Field] = $field
    }
}

$catalogByField = @{}; foreach ($row in $catalog) { $catalogByField[$row.field] = $row }
foreach ($field in $discovered.Values) {
    if (-not $catalogByField.ContainsKey($field.Field)) {
        Write-ReachabilityError "UNREGISTERED authored field '$($field.Field)' at $($field.FileLine). Wrong: an author can set this field but no runtime consumer is proven. Runtime actually does nothing until a named battle/meta/persistence/presentation consumer is cataloged. Choose one disposition: wire it / delete it / mark it, then add consumer evidence and the recursive nested surface to field-catalog.tsv."
    }
}
foreach ($row in $catalog) {
    if (-not $discovered.ContainsKey($row.field)) {
        Write-ReachabilityError "Catalog field '$($row.field)' no longer exists in source. Wrong: stale evidence hides the current authored surface. Runtime actually follows the new source shape. Choose wire it / delete it / mark it, then reconcile field-catalog.tsv."
    }
}

# A new ScriptableObject under the canonical authored root must name an audited top-level type.
$metaByGuid = @{}
Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'Assets') -Recurse -Filter '*.cs.meta' | ForEach-Object {
    $match = [regex]::Match([IO.File]::ReadAllText($_.FullName), '(?m)^guid:\s*(\S+)')
    if ($match.Success) {
        $relative = $_.FullName.Substring($RepoRoot.Length + 1).Replace('\', '/')
        $metaByGuid[$match.Groups[1].Value] = $relative.Substring(0, $relative.Length - 5)
    }
}
$auditedTopSources = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($entry in $typeSource.GetEnumerator()) {
    $simpleName = $entry.Key.Substring($entry.Key.LastIndexOf('.') + 1)
    if ([IO.Path]::GetFileNameWithoutExtension($entry.Value) -eq $simpleName) { [void]$auditedTopSources.Add($entry.Value) }
}
Get-ChildItem -LiteralPath $definitionRoot -Recurse -Filter '*.asset' | ForEach-Object {
    $match = [regex]::Match([IO.File]::ReadAllText($_.FullName), 'm_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]+)')
    if ($match.Success -and $metaByGuid.ContainsKey($match.Groups[1].Value)) {
        $scriptPath = $metaByGuid[$match.Groups[1].Value]
        if (-not $auditedTopSources.Contains($scriptPath)) {
            $relativeAsset = $_.FullName.Substring($RepoRoot.Length + 1).Replace('\', '/')
            Write-ReachabilityError "UNREGISTERED authored definition script '$scriptPath' used by '$relativeAsset'. Wrong: its fields have no reachability audit. Runtime actually ignores any field without a named consumer. Choose wire it / delete it / mark it, then register the top type and all recursively serialized fields."
        }
    }
}

# Dead is an enforceable catalog marker. Keep the warning bounded: Unity serializes defaults too.
$yamlKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
Get-ChildItem -LiteralPath $definitionRoot -Recurse -Filter '*.asset' | ForEach-Object {
    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($_.FullName), '(?m)^\s*(?<key>[A-Za-z_]\w*):')) {
        [void]$yamlKeys.Add($match.Groups['key'].Value)
    }
}
$authoredDead = @($catalog | Where-Object {
    $_.classification -eq 'dead' -and $yamlKeys.Contains($_.field.Substring($_.field.LastIndexOf('.') + 1))
})
if ($authoredDead.Count -gt 0) {
    $sample = ($authoredDead | Select-Object -First 8 | ForEach-Object field) -join ', '
    Write-Warning "[content reachability dead marker] $($authoredDead.Count) dead field markers match serialized YAML keys (sample: $sample). Wrong: authors can mistake these values for mechanics. Runtime actually has no consumer and does nothing. Choose wire it / delete it / mark it; these remain explicitly marked in field-catalog.tsv until a separately ratified deletion or wiring."
}

foreach ($fallback in $fallbacks) {
    if ($fallback.classification -notin @('legitimate-sentinel', 'trap')) {
        Write-ReachabilityError "Fallback '$($fallback.authored_field)' has invalid classification '$($fallback.classification)'. Wrong: the sentinel's meaning is unknown. Runtime actually follows '$($fallback.falls_back_to)'. Choose wire it / delete it / mark it, then classify it."
        continue
    }

    if ($fallback.classification -ne 'trap') { continue }
    if ($fallback.guard_message -notmatch 'Wrong:' -or $fallback.guard_message -notmatch 'Runtime actually' -or $fallback.guard_message -notmatch 'wire it / delete it / mark it') {
        Write-ReachabilityError "Trap '$($fallback.authored_field)' lacks a knowledge-bearing guard message. Wrong: the warning would omit the real behavior. Runtime actually follows '$($fallback.falls_back_to)'. Choose wire it / delete it / mark it, then repair guard_message."
    }

    if ($fallback.guard_rule -match '^existing-validator:') { continue }
    $assetPath = Join-Path $RepoRoot $fallback.asset_glob
    $assetDirectory = Split-Path $assetPath -Parent
    $assetPattern = Split-Path $assetPath -Leaf
    if (-not (Test-Path -LiteralPath $assetDirectory)) { continue }
    $fieldName = $fallback.authored_field.Substring($fallback.authored_field.LastIndexOf('.') + 1)
    $trapAssets = [System.Collections.Generic.List[string]]::new()
    foreach ($asset in Get-ChildItem -LiteralPath $assetDirectory -Recurse -Filter $assetPattern) {
        $text = [IO.File]::ReadAllText($asset.FullName)
        if ($fallback.guard_rule -eq 'missing') {
            if ($text -notmatch "(?m)^\s*$([regex]::Escape($fieldName))\s*:") {
                $trapAssets.Add($asset.FullName.Substring($RepoRoot.Length + 1).Replace('\', '/'))
            }
            continue
        }

        foreach ($valueMatch in [regex]::Matches($text, "(?m)^\s*$([regex]::Escape($fieldName))\s*:\s*(?<value>[^\r\n#]*)")) {
            if (Test-TrapValue -Rule $fallback.guard_rule -RawValue $valueMatch.Groups['value'].Value) {
                $trapAssets.Add($asset.FullName.Substring($RepoRoot.Length + 1).Replace('\', '/'))
                break
            }
        }
    }

    if ($trapAssets.Count -gt 0) {
        $sample = ($trapAssets | Select-Object -First 3) -join ', '
        Write-Warning "[content fallback trap] $($fallback.guard_message) Observed in $($trapAssets.Count) asset(s); sample: $sample."
    }
}

if ($failed) { exit 1 }
Write-Host "  PASS: $($catalog.Count) authored fields have explicit reachability dispositions; fallback traps are registered." -ForegroundColor Green
exit 0

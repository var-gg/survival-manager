[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$definitionsRoot = Join-Path $RepoRoot "Assets/Resources/_Game/Content/Definitions"
$declarationPath = Join-Path $PSScriptRoot "known-missing-art.tsv"
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$references = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)

function Read-Scalar {
    param([string[]]$Lines, [string]$Field)

    $prefix = "  ${Field}:"
    $line = @($Lines | Where-Object { $_.StartsWith($prefix, [System.StringComparison]::Ordinal) }) |
        Select-Object -First 1
    if ($null -eq $line) {
        return ""
    }

    return $line.Substring($prefix.Length).Trim()
}

function Resolve-ItemIconId {
    param([string[]]$Lines, [string]$ContentId)

    $authored = Read-Scalar -Lines $Lines -Field "IconId"
    if (-not [string]::IsNullOrWhiteSpace($authored)) {
        return $authored
    }

    $slotType = Read-Scalar -Lines $Lines -Field "SlotType"
    if ($slotType -eq "0") {
        $family = Read-Scalar -Lines $Lines -Field "WeaponFamilyTag"
        if ($family -notin @("shield", "bow", "focus", "blade")) {
            if ($ContentId.Contains("shield", [System.StringComparison]::Ordinal)) {
                $family = "shield"
            }
            elseif ($ContentId.Contains("bow", [System.StringComparison]::Ordinal)) {
                $family = "bow"
            }
            elseif ($ContentId.Contains("focus", [System.StringComparison]::Ordinal) -or
                    $ContentId.Contains("bead", [System.StringComparison]::Ordinal)) {
                $family = "focus"
            }
            else {
                $family = "blade"
            }
        }

        return "item_icon_$family"
    }

    if ($slotType -eq "1") {
        return "item_icon_armor"
    }

    return "item_icon_trinket"
}

if (-not (Test-Path -LiteralPath $declarationPath -PathType Leaf)) {
    throw "Icon routing declaration registry missing: $declarationPath"
}

$declaredRows = @(Import-Csv -LiteralPath $declarationPath -Delimiter "`t")
$declaredByKey = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
foreach ($row in $declaredRows) {
    $rowKey = "$($row.content_type)|$($row.content_id)|$($row.icon_key)"
    if ($declaredByKey.ContainsKey($rowKey)) {
        $failures.Add("Duplicate known-missing-art declaration: $rowKey")
        continue
    }

    $declaredByKey[$rowKey] = $row
}

$keySpaces = @(
    @{ ContentType = "skill"; Folder = "Skills"; ArtFolder = "Skill" },
    @{ ContentType = "item"; Folder = "Items"; ArtFolder = "Item" },
    @{ ContentType = "augment"; Folder = "Augments"; ArtFolder = "Augment" },
    @{ ContentType = "affix"; Folder = "Affixes"; ArtFolder = "Affix" }
)

$resolvedCount = 0
$declaredMissingCount = 0
foreach ($keySpace in $keySpaces) {
    $definitionFolder = Join-Path $definitionsRoot $keySpace.Folder
    foreach ($asset in @(Get-ChildItem -LiteralPath $definitionFolder -Filter "*.asset" -File | Sort-Object Name)) {
        $lines = @(Get-Content -LiteralPath $asset.FullName)
        $contentId = Read-Scalar -Lines $lines -Field "Id"
        $iconId = Read-Scalar -Lines $lines -Field "IconId"

        if ([string]::IsNullOrWhiteSpace($contentId)) {
            $failures.Add("ICON ROUTING FAIL content_id='<empty>' icon_key='<unknown>' expected_path='$($asset.FullName)': definition has no Id.")
            continue
        }

        switch ($keySpace.ContentType) {
            "skill" {
                if ([string]::IsNullOrWhiteSpace($iconId)) {
                    $suffix = if ($contentId.StartsWith("skill_", [System.StringComparison]::Ordinal)) {
                        $contentId.Substring("skill_".Length)
                    }
                    else {
                        $contentId
                    }
                    $iconId = "skill_icon_$suffix"
                }
            }
            "item" {
                $iconId = Resolve-ItemIconId -Lines $lines -ContentId $contentId
            }
            "augment" {
                if ([string]::IsNullOrWhiteSpace($iconId)) {
                    $iconId = if ($contentId.StartsWith("augment_", [System.StringComparison]::Ordinal)) {
                        $contentId
                    }
                    else {
                        "augment_$contentId"
                    }
                }
            }
            "affix" {
                if ([string]::IsNullOrWhiteSpace($iconId)) {
                    $expectedDirectory = "Assets/Resources/_Game/Art/Icons/Affix"
                    $failures.Add("ICON ROUTING FAIL content_id='$contentId' icon_key='<empty>' expected_path='$expectedDirectory/<IconId>.png': AffixDefinition.IconId must be authored.")
                    continue
                }
            }
        }

        $relativeExpectedPath = "Assets/Resources/_Game/Art/Icons/$($keySpace.ArtFolder)/$iconId.png"
        $absoluteExpectedPath = Join-Path $RepoRoot $relativeExpectedPath
        $referenceKey = "$($keySpace.ContentType)|$contentId|$iconId"
        $references[$referenceKey] = [pscustomobject]@{
            ContentType = $keySpace.ContentType
            ContentId = $contentId
            IconId = $iconId
            ExpectedPath = $relativeExpectedPath
        }

        if (Test-Path -LiteralPath $absoluteExpectedPath -PathType Leaf) {
            $resolvedCount++
            if ($declaredByKey.ContainsKey($referenceKey)) {
                $failures.Add("Stale known-missing-art declaration for '$referenceKey': asset now exists at '$relativeExpectedPath'; remove the declaration.")
            }
            continue
        }

        $declaredRow = $null
        if ($declaredByKey.TryGetValue($referenceKey, [ref]$declaredRow)) {
            if ($declaredRow.expected_path -ne $relativeExpectedPath) {
                $failures.Add("Known-missing-art path mismatch for '$referenceKey': declared='$($declaredRow.expected_path)' actual='$relativeExpectedPath'.")
                continue
            }

            $declaredMissingCount++
            $warnings.Add("ICON ROUTING DECLARED MISSING content_id='$contentId' icon_key='$iconId' expected_path='$relativeExpectedPath' reason='$($declaredRow.reason)'")
            continue
        }

        $failures.Add("ICON ROUTING FAIL content_id='$contentId' icon_key='$iconId' expected_path='$relativeExpectedPath': authored icon does not resolve. Add the asset or explicitly declare it in tools/icon-routing/known-missing-art.tsv.")
    }
}

$siteEventDefinitionFolder = Join-Path $definitionsRoot "SiteEvents"
foreach ($asset in @(Get-ChildItem -LiteralPath $siteEventDefinitionFolder -Filter "*.asset" -File | Sort-Object Name)) {
    $lines = @(Get-Content -LiteralPath $asset.FullName)
    $eventId = Read-Scalar -Lines $lines -Field "Id"
    if ([string]::IsNullOrWhiteSpace($eventId)) {
        $failures.Add("ICON ROUTING FAIL content_id='<empty>' icon_key='<unknown>' expected_path='$($asset.FullName)': site event definition has no Id.")
        continue
    }

    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        if ($lines[$lineIndex] -notmatch '^  - Id:\s*(?<choiceId>\S.*)$') {
            continue
        }

        $choiceId = $Matches.choiceId.Trim()
        $iconId = ""
        for ($choiceLineIndex = $lineIndex + 1; $choiceLineIndex -lt $lines.Count; $choiceLineIndex++) {
            if ($lines[$choiceLineIndex] -match '^  - Id:') {
                break
            }

            if ($lines[$choiceLineIndex] -match '^    IconId:\s*(?<iconId>.*)$') {
                $iconId = $Matches.iconId.Trim()
                break
            }
        }

        $contentId = "$eventId/$choiceId"
        if ([string]::IsNullOrWhiteSpace($iconId)) {
            $expectedDirectory = "Assets/Resources/_Game/Art/Icons/SiteEventChoice"
            $failures.Add("ICON ROUTING FAIL content_id='$contentId' icon_key='<empty>' expected_path='$expectedDirectory/<IconId>.png': SiteEventChoiceDefinition.IconId must be authored.")
            continue
        }

        $relativeExpectedPath = "Assets/Resources/_Game/Art/Icons/SiteEventChoice/$iconId.png"
        $absoluteExpectedPath = Join-Path $RepoRoot $relativeExpectedPath
        $referenceKey = "site_event_choice|$contentId|$iconId"
        $references[$referenceKey] = [pscustomobject]@{
            ContentType = "site_event_choice"
            ContentId = $contentId
            IconId = $iconId
            ExpectedPath = $relativeExpectedPath
        }

        if (Test-Path -LiteralPath $absoluteExpectedPath -PathType Leaf) {
            $resolvedCount++
            if ($declaredByKey.ContainsKey($referenceKey)) {
                $failures.Add("Stale known-missing-art declaration for '$referenceKey': asset now exists at '$relativeExpectedPath'; remove the declaration.")
            }
            continue
        }

        $declaredRow = $null
        if ($declaredByKey.TryGetValue($referenceKey, [ref]$declaredRow)) {
            if ($declaredRow.expected_path -ne $relativeExpectedPath) {
                $failures.Add("Known-missing-art path mismatch for '$referenceKey': declared='$($declaredRow.expected_path)' actual='$relativeExpectedPath'.")
                continue
            }

            $declaredMissingCount++
            $warnings.Add("ICON ROUTING DECLARED MISSING content_id='$contentId' icon_key='$iconId' expected_path='$relativeExpectedPath' reason='$($declaredRow.reason)'")
            continue
        }

        $failures.Add("ICON ROUTING FAIL content_id='$contentId' icon_key='$iconId' expected_path='$relativeExpectedPath': authored icon does not resolve. Add the asset or explicitly declare it in tools/icon-routing/known-missing-art.tsv.")
    }
}

foreach ($entry in $declaredByKey.GetEnumerator()) {
    if (-not $references.ContainsKey($entry.Key)) {
        $failures.Add("Unknown known-missing-art declaration '$($entry.Key)': no authored content reference matches it.")
    }
}

foreach ($warning in $warnings) {
    Write-Warning $warning
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Error $failure
    }
    exit 1
}

Write-Host "Icon routing lint passed: resolved=$resolvedCount declared_missing=$declaredMissingCount authored_references=$($references.Count)."

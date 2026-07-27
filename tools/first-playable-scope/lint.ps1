param(
    [string]$RepoRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)

$ErrorActionPreference = 'Stop'
$exitCode = 0
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).ProviderPath
$sliceRelativePath = 'Assets/Resources/_Game/Content/Definitions/FirstPlayable/first_playable_slice.asset'
$slicePath = Join-Path $RepoRoot $sliceRelativePath
$definitionsRoot = Join-Path $RepoRoot 'Assets/Resources/_Game/Content/Definitions'

function Write-ScopeFailure {
    param(
        [string]$Axis,
        [string]$Id,
        [string]$Issue,
        [string]$Action
    )

    Write-Host "SCOPE FAIL axis=$Axis id=$Id issue=$Issue action=`"$Action`"" -ForegroundColor Red
    $script:exitCode = 1
}

function Get-UnityScalar {
    param(
        [string]$Path,
        [string]$Field
    )

    $escapedField = [regex]::Escape($Field)
    foreach ($line in Get-Content -LiteralPath $Path) {
        $match = [regex]::Match($line, "^  ${escapedField}:\s*(.*)$")
        if ($match.Success) {
            return $match.Groups[1].Value.Trim()
        }
    }

    throw "Missing Unity YAML scalar '$Field' in '$Path'."
}

function Get-UnityList {
    param(
        [string]$Path,
        [string]$Field
    )

    $values = [System.Collections.Generic.List[string]]::new()
    $header = "  ${Field}:"
    $inside = $false
    foreach ($line in Get-Content -LiteralPath $Path) {
        if (-not $inside) {
            if ($line -eq $header) {
                $inside = $true
            }
            continue
        }

        $item = [regex]::Match($line, '^  -\s+(.+)$')
        if ($item.Success) {
            $values.Add($item.Groups[1].Value.Trim())
            continue
        }

        if ($line -match '^  [A-Za-z_][A-Za-z0-9_]*:') {
            break
        }
    }

    if (-not $inside) {
        throw "Missing Unity YAML list '$Field' in '$Path'."
    }

    return $values.ToArray()
}

function New-OrdinalSet {
    return ,([System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal))
}

function Add-AuthoredRecord {
    param(
        [System.Collections.Generic.List[object]]$Records,
        [string]$Axis,
        [string]$Path
    )

    $id = Get-UnityScalar -Path $Path -Field 'Id'
    $Records.Add([pscustomobject]@{
        Axis = $Axis
        Id = $id
        Path = $Path
    })
}

try {
    if (-not (Test-Path -LiteralPath $slicePath)) {
        Write-ScopeFailure -Axis 'ScopeContract' -Id $sliceRelativePath -Issue 'asset-missing' -Action 'Restore or create the first playable scope asset before changing capped content.'
        exit $exitCode
    }

    $axisSpecs = @(
        [pscustomobject]@{ Axis = 'UnitBlueprint'; CapField = 'UnitBlueprintCap'; ListField = 'UnitBlueprintIds' },
        [pscustomobject]@{ Axis = 'SignatureActive'; CapField = 'SignatureActiveCap'; ListField = 'SignatureActiveIds' },
        [pscustomobject]@{ Axis = 'SignaturePassive'; CapField = 'SignaturePassiveCap'; ListField = 'SignaturePassiveIds' },
        [pscustomobject]@{ Axis = 'FlexActive'; CapField = 'FlexActiveCap'; ListField = 'FlexActiveIds' },
        [pscustomobject]@{ Axis = 'FlexPassive'; CapField = 'FlexPassiveCap'; ListField = 'FlexPassiveIds' },
        [pscustomobject]@{ Axis = 'Affix'; CapField = 'AffixCap'; ListField = 'AffixIds' },
        [pscustomobject]@{ Axis = 'SynergyFamily'; CapField = 'SynergyFamilyCap'; ListField = 'SynergyFamilyIds' },
        [pscustomobject]@{ Axis = 'TemporaryAugment'; CapField = 'TemporaryAugmentCap'; ListField = 'TemporaryAugmentIds' },
        [pscustomobject]@{ Axis = 'PermanentAugment'; CapField = 'PermanentAugmentCap'; ListField = 'PermanentAugmentIds' },
        [pscustomobject]@{ Axis = 'PassiveBoard'; CapField = 'PassiveBoardCap'; ListField = 'PassiveBoardIds' }
    )

    $liveByAxis = @{}
    $allLive = New-OrdinalSet
    foreach ($spec in $axisSpecs) {
        $capText = Get-UnityScalar -Path $slicePath -Field $spec.CapField
        $cap = 0
        if (-not [int]::TryParse($capText, [ref]$cap)) {
            Write-ScopeFailure -Axis $spec.Axis -Id $spec.CapField -Issue 'cap-not-integer' -Action "Set $($spec.CapField) to the explicit live list length."
            continue
        }

        $ids = @(Get-UnityList -Path $slicePath -Field $spec.ListField)
        if ($cap -ne $ids.Count) {
            Write-ScopeFailure -Axis $spec.Axis -Id $spec.ListField -Issue "cap-list-mismatch(cap=$cap,count=$($ids.Count))" -Action "Update $($spec.CapField) and $($spec.ListField) together as one explicit scope decision."
        }

        $axisLive = New-OrdinalSet
        foreach ($id in $ids) {
            if (-not $axisLive.Add($id)) {
                Write-ScopeFailure -Axis $spec.Axis -Id $id -Issue 'duplicate-live-id' -Action "Remove the duplicate from $($spec.ListField)."
            }
            [void]$allLive.Add($id)
        }
        $liveByAxis[$spec.Axis] = $axisLive
    }

    $parking = New-OrdinalSet
    foreach ($id in @(Get-UnityList -Path $slicePath -Field 'ParkingLotContentIds')) {
        if (-not $parking.Add($id)) {
            Write-ScopeFailure -Axis 'ParkingLot' -Id $id -Issue 'duplicate-parking-id' -Action 'Keep each parked content id exactly once.'
        }
        if ($allLive.Contains($id)) {
            $liveAxis = @($axisSpecs | Where-Object { $liveByAxis[$_.Axis].Contains($id) } | Select-Object -ExpandProperty Axis) -join ','
            Write-ScopeFailure -Axis $liveAxis -Id $id -Issue 'live-and-parked' -Action 'Choose live or parked, remove the id from the other list, and commit that scope decision.'
        }
    }

    $authored = [System.Collections.Generic.List[object]]::new()

    foreach ($path in Get-ChildItem -LiteralPath (Join-Path $definitionsRoot 'Archetypes') -Filter '*.asset' -File) {
        Add-AuthoredRecord -Records $authored -Axis 'UnitBlueprint' -Path $path.FullName
    }

    $skillAxes = @{
        '0' = 'SignatureActive'
        '1' = 'FlexActive'
        '2' = 'SignaturePassive'
        '3' = 'FlexPassive'
    }
    foreach ($path in Get-ChildItem -LiteralPath (Join-Path $definitionsRoot 'Skills') -Filter '*.asset' -File) {
        $slotKind = Get-UnityScalar -Path $path.FullName -Field 'SlotKind'
        if (-not $skillAxes.ContainsKey($slotKind)) {
            $id = Get-UnityScalar -Path $path.FullName -Field 'Id'
            Write-ScopeFailure -Axis 'Skill' -Id $id -Issue "unknown-slot-kind($slotKind)" -Action 'Assign a supported SlotKind before classifying the skill as live or parked.'
            continue
        }
        Add-AuthoredRecord -Records $authored -Axis $skillAxes[$slotKind] -Path $path.FullName
    }

    foreach ($path in Get-ChildItem -LiteralPath (Join-Path $definitionsRoot 'Affixes') -Filter '*.asset' -File) {
        Add-AuthoredRecord -Records $authored -Axis 'Affix' -Path $path.FullName
    }

    foreach ($path in Get-ChildItem -LiteralPath (Join-Path $definitionsRoot 'Augments') -Filter '*.asset' -File) {
        $isPermanent = Get-UnityScalar -Path $path.FullName -Field 'IsPermanent'
        $axis = if ($isPermanent -eq '1') { 'PermanentAugment' } else { 'TemporaryAugment' }
        Add-AuthoredRecord -Records $authored -Axis $axis -Path $path.FullName
    }

    foreach ($path in Get-ChildItem -LiteralPath (Join-Path $definitionsRoot 'Synergies') -Filter 'synergy_*.asset' -File) {
        Add-AuthoredRecord -Records $authored -Axis 'SynergyFamily' -Path $path.FullName
    }

    foreach ($path in Get-ChildItem -LiteralPath (Join-Path $definitionsRoot 'PassiveBoards') -Filter '*.asset' -File) {
        Add-AuthoredRecord -Records $authored -Axis 'PassiveBoard' -Path $path.FullName
    }

    foreach ($record in $authored) {
        if ($liveByAxis[$record.Axis].Contains($record.Id) -or $parking.Contains($record.Id)) {
            continue
        }

        if ($allLive.Contains($record.Id)) {
            Write-ScopeFailure -Axis $record.Axis -Id $record.Id -Issue 'wrong-live-axis' -Action 'Move the id to the matching live axis or park it explicitly.'
        }
        else {
            Write-ScopeFailure -Axis $record.Axis -Id $record.Id -Issue 'neither-live-nor-parked' -Action 'Add the id to the matching live list with its cap update, or add it to ParkingLotContentIds, then commit the decision.'
        }
    }
}
catch {
    Write-ScopeFailure -Axis 'ScopeHarness' -Id $sliceRelativePath -Issue 'parse-or-scan-error' -Action "Repair the scope asset or lint parser. Detail: $($_.Exception.Message) Trace: $($_.ScriptStackTrace)"
}

if ($exitCode -eq 0) {
    Write-Host '  PASS: Every capped authored content id is explicitly live or parked, with exact caps and no overlap.' -ForegroundColor Green
}

exit $exitCode

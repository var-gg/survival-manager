param(
    [string]$RepoRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).ProviderPath
$failed = $false

function Get-RelativePath {
    param([string]$Path)
    return $Path.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
}

function Write-PlayerTextFailure {
    param(
        [string]$Surface,
        [string]$String,
        [string]$Action
    )

    $script:failed = $true
    Write-Host "PLAYER-FACING TEXT FAIL surface='$Surface' string='$String' action='$Action'" -ForegroundColor Red
}

$runtimeRoot = Join-Path $RepoRoot 'Assets/_Game/Scripts/Runtime/Unity'
$seedRoot = Join-Path $RepoRoot 'Assets/_Game/Scripts/Editor/Bootstrap'
$keyRegex = [regex]'"(?<key>ui\.(?:common|town|expedition|battle|reward)\.[A-Za-z0-9_.-]+)"'
$seedRegex = [regex]'^\s*\["(?<key>ui\.(?:common|town|expedition|battle|reward)\.[^"]+)"\]\s*=\s*\("(?<ko>(?:\\.|[^"])*)",\s*"(?<en>(?:\\.|[^"])*)",\s*(?:true|false)\),'

$runtimeKeys = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
foreach ($file in Get-ChildItem -LiteralPath $runtimeRoot -Filter '*.cs' -File -Recurse) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        foreach ($match in $keyRegex.Matches($line)) {
            $key = $match.Groups['key'].Value
            if (-not $runtimeKeys.ContainsKey($key)) {
                $runtimeKeys[$key] = [pscustomobject]@{
                    Path = Get-RelativePath $file.FullName
                    Line = $lineNumber
                }
            }
        }
    }
}

$seedEntries = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
foreach ($file in Get-ChildItem -LiteralPath $seedRoot -Filter 'LocalizationFoundationBootstrap*.cs' -File) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        $match = $seedRegex.Match($line)
        if (-not $match.Success) {
            continue
        }

        $key = $match.Groups['key'].Value
        $seedEntries[$key] = [pscustomobject]@{
            Ko = $match.Groups['ko'].Value
            En = $match.Groups['en'].Value
            Path = Get-RelativePath $file.FullName
            Line = $lineNumber
        }
    }
}

$languageNeutralKeys = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        'ui.common.on',
        'ui.common.off',
        'ui.battle.action.speed_05',
        'ui.battle.action.speed_1',
        'ui.battle.action.speed_2',
        'ui.battle.action.speed_4',
        'ui.reward.loot.entry',
        'ui.town.compendium.metric.id'
    ),
    [System.StringComparer]::Ordinal)

foreach ($key in ($runtimeKeys.Keys | Sort-Object)) {
    $surface = $runtimeKeys[$key]
    $surfaceName = "$($surface.Path):$($surface.Line)"
    if (-not $seedEntries.ContainsKey($key)) {
        Write-PlayerTextFailure `
            -Surface $surfaceName `
            -String $key `
            -Action 'Add Korean and English seed entries, run LocalizationFoundationBootstrap.EnsureFoundationAssets, and keep the UI fallback developer-only.'
        continue
    }

    $seed = $seedEntries[$key]
    if ([string]::IsNullOrWhiteSpace($seed.Ko)) {
        Write-PlayerTextFailure `
            -Surface "$($seed.Path):$($seed.Line)" `
            -String $key `
            -Action 'Provide a non-empty Korean seed and regenerate the serialized localization tables.'
        continue
    }

    $isCopiedEnglish = $seed.Ko -ceq $seed.En -and $seed.Ko -match '[A-Za-z]' -and -not $languageNeutralKeys.Contains($key)
    if ($isCopiedEnglish) {
        Write-PlayerTextFailure `
            -Surface "$($seed.Path):$($seed.Line)" `
            -String "$key => $($seed.Ko)" `
            -Action 'Replace the copied English Korean seed with player-ready Korean, then regenerate the serialized localization tables.'
    }
}

$sourceRules = @(
    [pscustomobject]@{
        Paths = @('Assets/_Game/Scripts/Runtime/Unity/ContentTextResolver.cs')
        Pattern = ':\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)?[A-Za-z_][A-Za-z0-9_]*Id\s*;'
        Action = 'Resolve an authored localized name or return an honest localized placeholder instead of the content id.'
    },
    [pscustomobject]@{
        Paths = @('Assets/_Game/Scripts/Runtime/Unity/UI')
        Pattern = '\b(?:Name|Label|Text|Summary|Title|Description|Value)\s*:\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)?[A-Za-z_][A-Za-z0-9_]*Id\b'
        Action = 'Map the identifier through authored content localization, or render a localized unknown placeholder.'
    },
    [pscustomobject]@{
        Paths = @('Assets/_Game/Scripts/Runtime/Unity/UI')
        Pattern = '\b(?:Name|Label|Text|Summary|Title|Description|Value)\s*:\s*[^,\r\n]*\.ToString\(\)'
        Action = 'Map the enum value to a localization key instead of rendering Enum.ToString().'
    },
    [pscustomobject]@{
        Paths = @(
            'Assets/_Game/Scripts/Runtime/Unity/UI',
            'Assets/_Game/Scripts/Runtime/Unity/BattleScreenController.cs',
            'Assets/_Game/Scripts/Runtime/Unity/DeploymentSetupPanelView.cs'
        )
        Pattern = '\b(?:result|quote|failure)\.(?:Error|Reason|Diagnostic)\b|\b(?:exception|ex)\.Message\b'
        IgnorePattern = '\bDebug\.Log'
        Action = 'Keep the diagnostic in developer logging and map the structured cause to localized player wording.'
    },
    [pscustomobject]@{
        Paths = @(
            'Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleUnitMetadataFormatter.cs',
            'Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenView.cs'
        )
        Pattern = 'lines\.Add\(state\.SkillId\)|lines\.AddRange\(unit\.TacticRuleSummaries\)|_ => category\.ToString\(\)'
        Action = 'Replace internal skill, tactic, or enum metadata with an authored localized player readout.'
    },
    [pscustomobject]@{
        Paths = @(
            'Assets/_Game/Scripts/Runtime/Unity/UI/Reward/RewardScreenPresenter.cs',
            'Assets/_Game/Scripts/Runtime/Unity/DeploymentSetupPanelView.cs'
        )
        Pattern = '\bSelectedTeamPosture\b(?!\s*\))'
        Action = 'Route the posture through TeamPostureText.Resolve before rendering it.'
    },
    [pscustomobject]@{
        Paths = @('Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryPresenter.cs')
        Pattern = '\b(?:itemName|name)\s*=\s*(?:item\.)?ItemBaseId\s*;|\?\?\s*(?:itemDef\.Id|affixId)\b'
        Action = 'Use ContentTextResolver or a localized unknown placeholder; never use an item or affix id as display text.'
    }
)

foreach ($rule in $sourceRules) {
    foreach ($relativePath in $rule.Paths) {
        $fullPath = Join-Path $RepoRoot $relativePath
        $files = if (Test-Path -LiteralPath $fullPath -PathType Container) {
            Get-ChildItem -LiteralPath $fullPath -Filter '*.cs' -File -Recurse
        }
        elseif (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            @(Get-Item -LiteralPath $fullPath)
        }
        else {
            @()
        }

        foreach ($file in $files) {
            $lineNumber = 0
            foreach ($line in Get-Content -LiteralPath $file.FullName) {
                $lineNumber++
                $trimmed = $line.TrimStart()
                if ($trimmed.StartsWith('//') -or $trimmed.StartsWith('///') -or $trimmed.StartsWith('*')) {
                    continue
                }
                if ($rule.PSObject.Properties.Name -contains 'IgnorePattern' -and $line -match $rule.IgnorePattern) {
                    continue
                }

                $match = [regex]::Match($line, $rule.Pattern)
                if ($match.Success) {
                    Write-PlayerTextFailure `
                        -Surface "$(Get-RelativePath $file.FullName):$lineNumber" `
                        -String ($match.Value.Trim()) `
                        -Action $rule.Action
                }
            }
        }
    }
}

if ($failed) {
    exit 1
}

Write-Host "  PASS: $($runtimeKeys.Count) runtime UI keys are Korean-seeded; no guarded raw ids, enum names, diagnostics, or copied-English Korean seeds." -ForegroundColor Green
exit 0

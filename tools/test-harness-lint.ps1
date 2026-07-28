param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent)
)

<#
.SYNOPSIS
    테스트 하네스 preflight lint — CI 또는 로컬에서 커밋 전 검증.

.DESCRIPTION
    아래 항목을 검사한다:
    1. Runtime asmdef가 UnityEditor / AssetDatabase를 #if 가드 없이 참조하면 실패
    2. [Category("BatchOnly")]가 아닌 테스트 코드에서 직접 resource/content/session production bootstrap을 호출하면 실패
    3. [Category("FastUnit")] 테스트 코드에서 authored Unity object fixture를 사용하면 실패
    4. EditMode test class가 class-level execution category를 선언하지 않으면 실패
    5. 스크립트/문서에서 -quit를 -runTests와 같이 사용하면 실패
    6. Pindoc 소스여야 하는 imagegen 입력 Markdown이 repo-local 임시 파일로 생기면 실패
    7. record(struct/class)가 자기 타입을 반환하는 public 인스턴스 속성을 갖는데 ToString 오버라이드가 없으면 실패
       (합성 PrintMembers가 그 속성을 출력하며 무한 재귀 → StackOverflow. NUnit 실패 메시지/로그 포맷에서 프로세스가 죽는다)
    8. FinalUnits 요소의 Id를 원본 loadout id 리터럴과 직접 동등 비교하면 실패
       (BattleFactory가 EntityId를 "ally_{index}_{id}" / "enemy_{index}_{id}"로 접두사화하므로 원본 id 직접 비교는
        항상 false — 정산/측정이 조용히 허구를 생산한다. 실제 3회 발생: P0 HP/EXP 미반영, dossier fallenAllyIds,
        WarrantSeparability protect 생존율 0% 오진. EndsWith("_{id}") 또는 접두사 포함 리터럴을 쓸 것)
    9. authored IconId가 실제 PNG로 해석되지 않고 명시적 known-missing-art 선언도 없으면 실패
    10. first playable capped content가 live/parking scope contract에서 누락되거나 중복되면 실패
    11. scene/prefab에 직렬화된 MonoBehaviour가 file-scoped namespace를 사용하면 실패
    12. player-facing UI key의 Korean seed 누락/영문 복사, raw content id/enum/diagnostic 렌더링 패턴이면 실패
#>

$ErrorActionPreference = 'Continue'
$exitCode = 0
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).ProviderPath

function Write-LintError {
    param([string]$Check, [string]$File, [string]$Detail)
    Write-Host "LINT FAIL [$Check] $File" -ForegroundColor Red
    Write-Host "  $Detail" -ForegroundColor Yellow
    $script:exitCode = 1
}

# ────────────────────────────────────────────────
# Check 1: Runtime asmdef 소스에서 UnityEditor 참조 (#if 가드 없이)
# ────────────────────────────────────────────────

Write-Host "`n== Check 1: UnityEditor in runtime assemblies ==" -ForegroundColor Cyan
$check1Fail = $false

$runtimeDirs = @(
    'Assets/_Game/Scripts/Runtime/Core',
    'Assets/_Game/Scripts/Runtime/Content',
    'Assets/_Game/Scripts/Runtime/Combat',
    'Assets/_Game/Scripts/Runtime/Meta',
    'Assets/_Game/Scripts/Runtime/Unity'
)

foreach ($dir in $runtimeDirs) {
    $fullDir = Join-Path $RepoRoot $dir
    if (-not (Test-Path $fullDir)) { continue }

    $csFiles = Get-ChildItem $fullDir -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $csFiles) {
        $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
        $inEditorGuard = $false
        $lineNum = 0

        foreach ($line in $lines) {
            $lineNum++
            $trimmed = $line.Trim()

            if ($trimmed -match '^#if\s+UNITY_EDITOR') {
                $inEditorGuard = $true
                continue
            }
            if ($trimmed -match '^#endif' -and $inEditorGuard) {
                $inEditorGuard = $false
                continue
            }

            if ($trimmed.StartsWith('//') -or $trimmed.StartsWith('*') -or $trimmed.StartsWith('///') -or $trimmed.StartsWith('/*')) {
                continue
            }

            # UnityEditor and AssetDatabase outside #if UNITY_EDITOR guard
            if (-not $inEditorGuard -and ($trimmed -match '^\s*using\s+UnityEditor' -or $trimmed -match '\bUnityEditor\.' -or $trimmed -match '\bAssetDatabase\b')) {
                $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
                Write-LintError -Check 'UnityEditor-in-runtime' -File "$relPath`:$lineNum" -Detail "UnityEditor/AssetDatabase reference without #if UNITY_EDITOR guard"
                $check1Fail = $true
            }
        }
    }
}

if (-not $check1Fail) {
    Write-Host "  PASS: No unguarded UnityEditor/AssetDatabase references in runtime assemblies." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 2: direct resource/content/session bootstrap and FastUnit authored object tokens
# ────────────────────────────────────────────────

Write-Host "`n== Check 2: direct resource/content/session bootstrap and FastUnit authored object tokens ==" -ForegroundColor Cyan
$check2Fail = $false
$gameSessionFactoryAllowlist = @(
    'Assets/Tests/EditMode/FastUnit/Fakes/GameSessionTestFactory.cs'
)

$testDir = Join-Path $RepoRoot 'Assets/Tests/EditMode'
if (Test-Path $testDir) {
    $testFiles = Get-ChildItem $testDir -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $testFiles) {
        # 줄 단위로 읽어서 주석이 아닌 코드 라인만 검사
        $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
        if ($null -eq $lines) { continue }

        $codeContent = ($lines | Where-Object {
            $trimmed = $_.TrimStart()
            -not $trimmed.StartsWith('//') -and -not $trimmed.StartsWith('*') -and -not $trimmed.StartsWith('///') -and -not $trimmed.StartsWith('/*')
        }) -join [Environment]::NewLine

        $fullContent = Get-Content $file.FullName -Raw
        $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        $isBatchOnly = $fullContent -match '\[Category\(\s*"BatchOnly"\s*\)\]'
        $isFastUnit = $fullContent -match '\[Category\(\s*"FastUnit"\s*\)\]'
        $isGameSessionFactoryAllowlisted = $gameSessionFactoryAllowlist -contains $relPath

        # Test classes must declare their lane at class level. Method-level categories are not enough.
        if ($codeContent -match '\[(?:Test|TestCase|UnityTest)\b') {
            $classMatches = [regex]::Matches(
                $codeContent,
                '(?s)(?<attributes>(?:\[[^\]]+\]\s*)*)public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(?<name>\w+Tests)\b')

            foreach ($classMatch in $classMatches) {
                $attributes = $classMatch.Groups['attributes'].Value
                if ($attributes -notmatch '\[Category\(\s*"(FastUnit|BatchOnly|ManualLoopD)"\s*\)\]') {
                    Write-LintError -Check 'TestCategory-missing-class-level' -File $relPath -Detail "Test class $($classMatch.Groups['name'].Value) must declare class-level [Category(`"FastUnit`")], [Category(`"BatchOnly`")], or [Category(`"ManualLoopD`")]"
                    $check2Fail = $true
                }
            }
        }

        $resourcesPatternFound =
            $codeContent -match 'Resources\.Load(All)?\s*\(' -or
            $codeContent -match 'using\s+static\s+UnityEngine\.Resources\s*;' -or
            $codeContent -match 'using\s+\w+\s*=\s*UnityEngine\.Resources\s*;'

        # Resources.Load / Resources.LoadAll 또는 alias/static-import를 코드에서 직접 사용하는 경우
        if ($resourcesPatternFound) {
            if (-not $isBatchOnly) {
                Write-LintError -Check 'ResourcesLoad-outside-BatchOnly' -File $relPath -Detail "Resources.Load/LoadAll or Resources alias/static-import found but [Category(`"BatchOnly`")] missing on class"
                $check2Fail = $true
            }
        }

        $runtimeLookupPatternFound =
            $codeContent -match 'new\s+RuntimeCombatContentLookup\s*\(' -or
            $codeContent -match '\bRuntimeCombatContentLookup\s+\w+\s*\(' -or
            $codeContent -match 'using\s+\w+\s*=\s*SM\.Unity\.RuntimeCombatContentLookup\s*;'

        # RuntimeCombatContentLookup를 코드에서 직접 생성하거나 wrapper/alias로 숨기는 경우
        if ($runtimeLookupPatternFound) {
            if (-not $isBatchOnly) {
                Write-LintError -Check 'RuntimeLookup-outside-BatchOnly' -File $relPath -Detail "RuntimeCombatContentLookup construction/wrapper/alias found but [Category(`"BatchOnly`")] missing — use FakeCombatContentLookup or add [Category(`"BatchOnly`")]"
                $check2Fail = $true
            }
        }

        $narrativeResourcesPatternFound =
            $codeContent -match 'NarrativeRuntimeBootstrap\.LoadFromResources\s*\(' -or
            $codeContent -match 'using\s+\w+\s*=\s*SM\.Unity\.NarrativeRuntimeBootstrap\s*;' -or
            $codeContent -match '\bNarrativeRuntimeBootstrap\s+\w+\s*\('

        # NarrativeRuntimeBootstrap.LoadFromResources는 Resources-backed narrative catalog를 로드한다.
        if ($narrativeResourcesPatternFound) {
            if (-not $isBatchOnly) {
                Write-LintError -Check 'NarrativeResources-outside-BatchOnly' -File $relPath -Detail "NarrativeRuntimeBootstrap resource bootstrap/wrapper/alias found but [Category(`"BatchOnly`")] missing"
                $check2Fail = $true
            }
        }

        # GameSessionState public constructor는 production narrative Resources bootstrap을 수행한다.
        if ($codeContent -match 'new\s+GameSessionState\s*\(') {
            if (-not $isBatchOnly -and -not $isGameSessionFactoryAllowlisted) {
                Write-LintError -Check 'GameSessionState-outside-BatchOnly' -File $relPath -Detail "new GameSessionState() found outside BatchOnly — use GameSessionTestFactory.Create(...) for fast tests"
                $check2Fail = $true
            }
        }

        if ($isFastUnit) {
            $fastUnitForbiddenPatterns = @(
                @{ Check = 'ScriptableObject-in-FastUnit'; Pattern = 'ScriptableObject\.CreateInstance'; Detail = 'ScriptableObject.CreateInstance found in FastUnit — move authored-object coverage to BatchOnly or use pure fixtures' },
                @{ Check = 'UnityScriptableObject-in-FastUnit'; Pattern = 'UnityEngine\.ScriptableObject'; Detail = 'UnityEngine.ScriptableObject token found in FastUnit — move authored-object coverage to BatchOnly or use pure fixtures' },
                @{ Check = 'ScriptableObject-alias-in-FastUnit'; Pattern = 'using\s+\w+\s*=\s*UnityEngine\.ScriptableObject\s*;'; Detail = 'UnityEngine.ScriptableObject alias found in FastUnit — move authored-object coverage to BatchOnly or use pure fixtures' },
                @{ Check = 'UnityObject-in-FastUnit'; Pattern = 'UnityEngine\.Object'; Detail = 'UnityEngine.Object lifecycle found in FastUnit — move authored-object coverage to BatchOnly or use pure fixtures' },
                @{ Check = 'UnityObject-alias-in-FastUnit'; Pattern = 'using\s+\w+\s*=\s*UnityEngine\.Object\s*;'; Detail = 'UnityEngine.Object alias found in FastUnit — move authored-object coverage to BatchOnly or use pure fixtures' },
                @{ Check = 'UnityObjectLifecycle-in-FastUnit'; Pattern = 'Object\.(Instantiate|Destroy|DestroyImmediate)\s*\('; Detail = 'UnityEngine.Object lifecycle API found in FastUnit — move Unity object lifecycle coverage to BatchOnly' },
                @{ Check = 'DestroyImmediate-in-FastUnit'; Pattern = 'DestroyImmediate'; Detail = 'DestroyImmediate found in FastUnit — move Unity object lifecycle coverage to BatchOnly' },
                @{ Check = 'ContentDefinitions-in-FastUnit'; Pattern = 'using\s+SM\.Content\.Definitions'; Detail = 'SM.Content.Definitions import found in FastUnit — use pure snapshot/spec fixtures or BatchOnly' },
                @{ Check = 'ContentDefinitions-token-in-FastUnit'; Pattern = '\bSM\.Content\.Definitions\b'; Detail = 'SM.Content.Definitions token found in FastUnit — use pure snapshot/spec fixtures or BatchOnly' },
                @{ Check = 'ResourcesStaticImport-in-FastUnit'; Pattern = 'using\s+static\s+UnityEngine\.Resources\s*;'; Detail = 'UnityEngine.Resources static import found in FastUnit — move resource coverage to BatchOnly' },
                @{ Check = 'ResourcesAlias-in-FastUnit'; Pattern = 'using\s+\w+\s*=\s*UnityEngine\.Resources\s*;'; Detail = 'UnityEngine.Resources alias found in FastUnit — move resource coverage to BatchOnly' },
                @{ Check = 'RuntimeLookup-token-in-FastUnit'; Pattern = 'RuntimeCombatContentLookup'; Detail = 'RuntimeCombatContentLookup token found in FastUnit — production lookup coverage belongs in BatchOnly' },
                @{ Check = 'RuntimeLookup-alias-in-FastUnit'; Pattern = 'using\s+\w+\s*=\s*SM\.Unity\.RuntimeCombatContentLookup\s*;'; Detail = 'RuntimeCombatContentLookup alias found in FastUnit — production lookup coverage belongs in BatchOnly' }
            )

            foreach ($rule in $fastUnitForbiddenPatterns) {
                if ($codeContent -match $rule.Pattern) {
                    Write-LintError -Check $rule.Check -File $relPath -Detail $rule.Detail
                    $check2Fail = $true
                }
            }
        }
    }
}

if (-not $check2Fail -and $exitCode -eq 0) {
    Write-Host "  PASS: No direct resource/content/session bootstrap or FastUnit authored object tokens outside allowed lanes." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 3: ContentConversion ownership boundary
# ────────────────────────────────────────────────

Write-Host "`n== Check 3: ContentConversion ownership boundary ==" -ForegroundColor Cyan
$check3Fail = $false

$contentConversionDir = Join-Path $RepoRoot 'Assets/_Game/Scripts/Runtime/Unity/ContentConversion'
if (Test-Path $contentConversionDir) {
    $asmdefs = Get-ChildItem $contentConversionDir -Filter '*.asmdef' -Recurse -ErrorAction SilentlyContinue
    foreach ($asmdef in $asmdefs) {
        $relPath = $asmdef.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        Write-LintError -Check 'ContentConversion-asmdef' -File $relPath -Detail 'ContentConversion is currently an internal SM.Unity folder boundary; do not add a local asmdef without updating architecture docs and guards.'
        $check3Fail = $true
    }

    $registryRelPath = 'Assets/_Game/Scripts/Runtime/Unity/ContentConversion/ContentDefinitionRegistry.cs'
    $conversionFiles = Get-ChildItem $contentConversionDir -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $conversionFiles) {
        $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
        if ($null -eq $lines) { continue }

        $codeContent = ($lines | Where-Object {
            $trimmed = $_.TrimStart()
            -not $trimmed.StartsWith('//') -and -not $trimmed.StartsWith('*') -and -not $trimmed.StartsWith('///') -and -not $trimmed.StartsWith('/*')
        }) -join [Environment]::NewLine
        $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')

        if ($codeContent -notmatch 'namespace\s+SM\.Unity\.ContentConversion\s*;') {
            Write-LintError -Check 'ContentConversion-namespace' -File $relPath -Detail 'ContentConversion files must stay in namespace SM.Unity.ContentConversion.'
            $check3Fail = $true
        }

        $forbiddenAdapterPatterns = @(
            @{ Check = 'ContentConversion-public-api'; Pattern = '(?m)^\s*public\s+'; Detail = 'ContentConversion must not expose public API surface from the SM.Unity assembly.' },
            @{ Check = 'ContentConversion-persistence'; Pattern = '\bSM\.Persistence\b|\bSaveProfile\b'; Detail = 'ContentConversion must not own persistence or save profile truth.' },
            @{ Check = 'ContentConversion-session'; Pattern = '\bGameSessionState\b|\bSessionRealm\b'; Detail = 'ContentConversion must not own session facade flow.' },
            @{ Check = 'ContentConversion-runtime-lookup'; Pattern = '\bRuntimeCombatContentLookup\b'; Detail = 'ContentConversion must not construct or own production lookup.' },
            @{ Check = 'ContentConversion-presentation'; Pattern = '\bMonoBehaviour\b|UnityEngine\.SceneManagement|UnityEngine\.UIElements|\bUIDocument\b'; Detail = 'ContentConversion must not own scene/UI/presentation responsibilities.' }
        )

        foreach ($rule in $forbiddenAdapterPatterns) {
            if ($codeContent -match $rule.Pattern) {
                Write-LintError -Check $rule.Check -File $relPath -Detail $rule.Detail
                $check3Fail = $true
            }
        }

        if ($relPath -eq $registryRelPath) {
            continue
        }

        $assetLoadingPatterns = @(
            @{ Check = 'ContentConversion-resource-loading'; Pattern = 'Resources\.Load(All)?\s*\('; Detail = 'Asset loading must stay in ContentDefinitionRegistry.' },
            @{ Check = 'ContentConversion-assetdatabase'; Pattern = '\bAssetDatabase\b'; Detail = 'Editor asset sweep must stay in ContentDefinitionRegistry.' },
            @{ Check = 'ContentConversion-unityeditor'; Pattern = '(?m)^\s*using\s+UnityEditor\s*;'; Detail = 'UnityEditor import must stay in ContentDefinitionRegistry.' },
            @{ Check = 'ContentConversion-file-fallback'; Pattern = 'RuntimeCombatContentFileParser'; Detail = 'File fallback parser must stay in ContentDefinitionRegistry.' }
        )

        foreach ($rule in $assetLoadingPatterns) {
            if ($codeContent -match $rule.Pattern) {
                Write-LintError -Check $rule.Check -File $relPath -Detail $rule.Detail
                $check3Fail = $true
            }
        }
    }
}

if (-not $check3Fail -and $exitCode -eq 0) {
    Write-Host "  PASS: ContentConversion remains an internal SM.Unity authored-to-runtime adapter boundary." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 4: -quit combined with -runTests
# ────────────────────────────────────────────────

Write-Host "`n== Check 4: -quit with -runTests ==" -ForegroundColor Cyan
$check4Fail = $false

$scriptDirs = @('tools', '.github', '.codex')
$scriptExtensions = @('*.ps1', '*.sh', '*.yml', '*.yaml')

foreach ($dir in $scriptDirs) {
    $fullDir = Join-Path $RepoRoot $dir
    if (-not (Test-Path $fullDir)) { continue }

    foreach ($ext in $scriptExtensions) {
        $files = Get-ChildItem $fullDir -Filter $ext -Recurse -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            # lint 스크립트 자체는 패턴 문자열을 포함하므로 제외
            if ($file.Name -eq 'test-harness-lint.ps1') { continue }
            $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
            if ($null -eq $lines) { continue }

            # 주석이 아닌 연속 코드 블록에서만 -quit과 -runTests 동시 사용을 검사한다.
            # GitHub workflow의 license activation 명령처럼 같은 파일에 안전한 -quit가 별도 step으로 있을 수 있다.
            $blocks = New-Object System.Collections.Generic.List[string]
            $currentBlock = New-Object System.Collections.Generic.List[string]

            foreach ($line in $lines) {
                $trimmed = $line.TrimStart()
                $isComment = $trimmed.StartsWith('#') -or $trimmed.StartsWith('//') -or $trimmed.StartsWith('REM')
                if ($trimmed.Length -eq 0 -or $isComment) {
                    if ($currentBlock.Count -gt 0) {
                        $blocks.Add(($currentBlock -join ' '))
                        $currentBlock.Clear()
                    }
                    continue
                }

                $currentBlock.Add($line)
            }

            if ($currentBlock.Count -gt 0) {
                $blocks.Add(($currentBlock -join ' '))
            }

            foreach ($block in $blocks) {
                if ($block -match '-runTests' -and $block -match "'-quit'|`"-quit`"|\s-quit\b") {
                    $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
                    Write-LintError -Check 'quit-with-runTests' -File $relPath -Detail "-quit and -runTests found in the same executable code block — -quit can terminate Unity before tests finish"
                    $check4Fail = $true
                    break
                }
            }
        }
    }
}

if (-not $check4Fail -and $exitCode -eq 0) {
    Write-Host "  PASS: No -quit combined with -runTests." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 5: repo-local imagegen Markdown prompt spill
# ────────────────────────────────────────────────

Write-Host "`n== Check 5: repo-local imagegen Markdown prompt spill ==" -ForegroundColor Cyan
$check5Fail = $false

$forbiddenImagegenMarkdownDirs = @(
    'art-pipeline/working',
    'art-pipeline/subjects/ui_detail',
    'art-pipeline/subjects/ui_mockups'
)

foreach ($dir in $forbiddenImagegenMarkdownDirs) {
    $fullDir = Join-Path $RepoRoot $dir
    if (-not (Test-Path $fullDir)) { continue }

    $files = Get-ChildItem $fullDir -Filter '*.md' -Recurse -File -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        Write-LintError -Check 'ImagegenMarkdown-spill' -File $relPath -Detail 'Pindoc-owned imagegen prompt/style input must be hydrated as repo-external JSON or passed from outside the repo, not stored as local Markdown.'
        $check5Fail = $true
    }
}

$forbiddenImagegenMarkdownFiles = @(
    'art-pipeline/style/style-anchor-ui-detail.md',
    'art-pipeline/style/style-anchor-ui-mockup.md',
    'art-pipeline/subjects/backgrounds/ui_compendium/dusk_v2.md'
)

foreach ($file in $forbiddenImagegenMarkdownFiles) {
    $fullPath = Join-Path $RepoRoot $file
    if (Test-Path $fullPath) {
        Write-LintError -Check 'ImagegenMarkdown-spill' -File $file -Detail 'This generated prompt/style source belongs in Pindoc or repo-external transient input, not repository Markdown.'
        $check5Fail = $true
    }
}

if (-not $check5Fail -and $exitCode -eq 0) {
    Write-Host "  PASS: No repo-local Pindoc-owned imagegen Markdown prompt spill." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 6: record with self-typed public property must override ToString
# ────────────────────────────────────────────────

Write-Host "`n== Check 6: record self-typed property without ToString override ==" -ForegroundColor Cyan
$check6Fail = $false

$recordScanDirs = @('Assets/_Game/Scripts', 'Assets/Tests')
foreach ($dir in $recordScanDirs) {
    $fullDir = Join-Path $RepoRoot $dir
    if (-not (Test-Path $fullDir)) { continue }

    $csFiles = Get-ChildItem $fullDir -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $csFiles) {
        $raw = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if ([string]::IsNullOrEmpty($raw)) { continue }
        if ($raw -notmatch '\brecord\b') { continue }

        # 모든 타입 선언 위치를 잡아 record 본문 근사 경계로 쓴다(다음 타입 선언 전까지).
        $typeDecls = [regex]::Matches($raw, '\b(?:record\s+(?:struct\s+|class\s+)?|class\s+|struct\s+|interface\s+|enum\s+)(?<name>\w+)')
        for ($i = 0; $i -lt $typeDecls.Count; $i++) {
            if ($typeDecls[$i].Value -notmatch '^record\b') { continue }
            $name = $typeDecls[$i].Groups['name'].Value
            $bodyStart = $typeDecls[$i].Index
            $bodyEnd = if ($i + 1 -lt $typeDecls.Count) { $typeDecls[$i + 1].Index } else { $raw.Length }
            $body = $raw.Substring($bodyStart, $bodyEnd - $bodyStart)

            # 합성 PrintMembers는 public 인스턴스 속성을 전부 출력한다. 자기 타입을 반환하는
            # public 인스턴스 속성이 있으면 합성 ToString이 무한 재귀한다(static은 출력 안 되므로 제외).
            $selfTypedProperty = [regex]::IsMatch($body, "(?m)^\s*public\s+$name\s+\w+\s*(=>|\{|\r?$)")
            if ($selfTypedProperty -and $body -notmatch 'override\s+string\s+ToString') {
                $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
                Write-LintError -Check 'Record-selftyped-property-no-ToString' -File $relPath -Detail "record '$name' has a public instance property of its own type — synthesized ToString/PrintMembers recurses infinitely (StackOverflow). Add an explicit ToString() override."
                $check6Fail = $true
            }
        }
    }
}

if (-not $check6Fail) {
    Write-Host "  PASS: No record with a self-typed public property missing a ToString override." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 7: FinalUnits Id direct comparison against raw loadout id literal
# ────────────────────────────────────────────────

Write-Host "`n== Check 7: FinalUnits Id raw-literal comparison (ally_/enemy_ prefix fiction) ==" -ForegroundColor Cyan
$check7Fail = $false

$finalUnitsScanDirs = @('Assets/_Game/Scripts', 'Assets/Tests')
foreach ($dir in $finalUnitsScanDirs) {
    $fullDir = Join-Path $RepoRoot $dir
    if (-not (Test-Path $fullDir)) { continue }

    $csFiles = Get-ChildItem $fullDir -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue
    foreach ($file in $csFiles) {
        $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
        if ($null -eq $lines) { continue }

        $codeContent = ($lines | Where-Object {
            $trimmed = $_.TrimStart()
            -not $trimmed.StartsWith('//') -and -not $trimmed.StartsWith('*') -and -not $trimmed.StartsWith('///') -and -not $trimmed.StartsWith('/*')
        }) -join [Environment]::NewLine

        if ($codeContent -notmatch 'FinalUnits') { continue }

        # FinalUnits가 등장하는 문장 안에서 .Id ==/!= "리터럴" 직접 비교를 잡는다.
        # BattleFactory 접두사("ally_"/"enemy_")로 시작하는 리터럴은 접두사를 인지한 비교라 허용.
        $fictionPattern = '(?s)FinalUnits[^;{]{0,240}?\.Id\s*[!=]=\s*"(?!ally_|enemy_)'
        if ([regex]::IsMatch($codeContent, $fictionPattern)) {
            $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
            Write-LintError -Check 'FinalUnits-raw-id-comparison' -File $relPath -Detail 'FinalUnits element Id compared to a raw loadout id literal — BattleFactory prefixes ids as "ally_{index}_{id}"/"enemy_{index}_{id}", so this comparison is always false and silently fabricates results. Use EndsWith("_{id}") or a prefixed literal.'
            $check7Fail = $true
        }
    }
}

if (-not $check7Fail) {
    Write-Host "  PASS: No FinalUnits raw-loadout-id comparisons." -ForegroundColor Green
}

# ────────────────────────────────────────────────
# Check 8: authored content field reachability + silent fallback registry
# ────────────────────────────────────────────────

Write-Host "`n== Check 8: Authored content runtime reachability and fallback traps ==" -ForegroundColor Cyan
$reachabilityLint = Join-Path $RepoRoot 'tools/content-reachability/lint.ps1'
if (-not (Test-Path -LiteralPath $reachabilityLint)) {
    Write-LintError -Check 'Authored-content-reachability' -File 'tools/content-reachability/lint.ps1' -Detail 'Reachability lint is missing. Wrong: newly authored fields can have no runtime consumer. Runtime actually ignores those values. Choose wire it / delete it / mark it before adding the field.'
}
else {
    & $reachabilityLint -RepoRoot $RepoRoot
    if ($LASTEXITCODE -ne 0) {
        Write-LintError -Check 'Authored-content-reachability' -File 'tools/content-reachability/field-catalog.tsv' -Detail 'Reachability catalog validation failed. Wrong: an authored field or sentinel lacks proven runtime behavior. Runtime actually ignores an unconsumed field or applies the registered fallback. Choose wire it / delete it / mark it, then repair the evidence above.'
    }
}

# ────────────────────────────────────────────────
# Check 9: authored icon identity resolves or is explicitly declared missing
# ────────────────────────────────────────────────

Write-Host "`n== Check 9: Authored icon routing ==" -ForegroundColor Cyan
$iconRoutingLint = Join-Path $RepoRoot 'tools/icon-routing/lint.ps1'
if (-not (Test-Path -LiteralPath $iconRoutingLint)) {
    Write-LintError -Check 'Authored-icon-routing' -File 'tools/icon-routing/lint.ps1' -Detail 'Icon routing lint is missing. Authored icon references must resolve or be explicitly declared in tools/icon-routing/known-missing-art.tsv.'
}
else {
    & $iconRoutingLint -RepoRoot $RepoRoot
    if ($LASTEXITCODE -ne 0) {
        Write-LintError -Check 'Authored-icon-routing' -File 'tools/icon-routing/known-missing-art.tsv' -Detail 'Authored icon routing validation failed. Repair the exact content id, icon key, and expected path reported above.'
    }
}

# ────────────────────────────────────────────────
# Check 10: first playable capped content is explicitly live or parked
# ────────────────────────────────────────────────

Write-Host "`n== Check 10: First playable scope contract ==" -ForegroundColor Cyan
$firstPlayableScopeLint = Join-Path $RepoRoot 'tools/first-playable-scope/lint.ps1'
if (-not (Test-Path -LiteralPath $firstPlayableScopeLint)) {
    Write-LintError -Check 'First-playable-scope' -File 'tools/first-playable-scope/lint.ps1' -Detail 'Scope lint is missing. Every capped authored content id must be explicitly live or parked.'
}
else {
    & $firstPlayableScopeLint -RepoRoot $RepoRoot
    if ($LASTEXITCODE -ne 0) {
        Write-LintError -Check 'First-playable-scope' -File 'Assets/Resources/_Game/Content/Definitions/FirstPlayable/first_playable_slice.asset' -Detail 'First playable scope validation failed. Repair the exact axis and id reported above, then commit the cap/list/parking decision together.'
    }
}

# ────────────────────────────────────────────────
# Check 11: serialized MonoBehaviours declare a block namespace
# ────────────────────────────────────────────────

Write-Host "`n== Check 11: Serialized MonoBehaviour namespace form ==" -ForegroundColor Cyan
$serializedNamespaceLint = Join-Path $RepoRoot 'tools/serialized-monobehaviour-namespace/lint.ps1'
if (-not (Test-Path -LiteralPath $serializedNamespaceLint)) {
    Write-LintError -Check 'Serialized-monobehaviour-namespace' -File 'tools/serialized-monobehaviour-namespace/lint.ps1' -Detail 'Namespace-form lint is missing. A MonoBehaviour serialized into a scene or prefab must use a block namespace or Unity silently drops its script binding.'
}
else {
    & $serializedNamespaceLint -RepoRoot $RepoRoot
    if ($LASTEXITCODE -ne 0) {
        Write-LintError -Check 'Serialized-monobehaviour-namespace' -File 'Assets/_Game/Scripts' -Detail 'A scene- or prefab-serialized MonoBehaviour uses a file-scoped namespace. Convert the exact file reported above to a block namespace; Unity 6000.4.7f1 leaves its MonoScript class-null and the component loads with a missing script.'
    }
}

# ────────────────────────────────────────────────
# Check 12: player-facing wording stays localized and semantic
# ────────────────────────────────────────────────

Write-Host "`n== Check 12: Player-facing localization and semantic text ==" -ForegroundColor Cyan
$playerFacingTextLint = Join-Path $RepoRoot 'tools/player-facing-text/lint.ps1'
if (-not (Test-Path -LiteralPath $playerFacingTextLint)) {
    Write-LintError -Check 'Player-facing-text' -File 'tools/player-facing-text/lint.ps1' -Detail 'Player-facing text lint is missing. UI localization keys, raw content ids, enum names, and structured failure boundaries must remain guarded.'
}
else {
    & $playerFacingTextLint -RepoRoot $RepoRoot
    if ($LASTEXITCODE -ne 0) {
        Write-LintError -Check 'Player-facing-text' -File 'tools/player-facing-text/lint.ps1' -Detail 'Player-facing text validation failed. Repair the exact surface, string, and action reported above.'
    }
}

# ────────────────────────────────────────────────
# Summary
# ────────────────────────────────────────────────

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "All test harness lint checks passed." -ForegroundColor Green
}
else {
    Write-Host "Test harness lint checks failed. Fix the issues above before committing." -ForegroundColor Red
}

exit $exitCode

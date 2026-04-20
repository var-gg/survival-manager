param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent)
)

<#
.SYNOPSIS
    테스트 하네스 preflight lint — CI 또는 로컬에서 커밋 전 검증.

.DESCRIPTION
    아래 세 가지를 검사한다:
    1. Runtime asmdef가 UnityEditor / AssetDatabase를 #if 가드 없이 참조하면 실패
    2. [Category("BatchOnly")]가 아닌 테스트 코드에서 직접 resource/content/session production bootstrap을 호출하면 실패
    3. [Category("FastUnit")] 테스트 코드에서 authored Unity object fixture를 사용하면 실패
    4. EditMode test class가 class-level execution category를 선언하지 않으면 실패
    5. 스크립트/문서에서 -quit를 -runTests와 같이 사용하면 실패
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

            # 주석이 아닌 코드 라인에서만 -quit과 -runTests 동시 사용을 검사
            $codeLines = $lines | Where-Object {
                $trimmed = $_.TrimStart()
                -not $trimmed.StartsWith('#') -and -not $trimmed.StartsWith('//') -and -not $trimmed.StartsWith('REM')
            }
            $codeContent = $codeLines -join ' '

            # 같은 코드 블록에서 -runTests와 -quit 모두 사용
            if ($codeContent -match '-runTests' -and $codeContent -match "'-quit'|`"-quit`"|\s-quit\b") {
                $relPath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
                Write-LintError -Check 'quit-with-runTests' -File $relPath -Detail "-quit and -runTests found in executable code — -quit can terminate Unity before tests finish"
                $check4Fail = $true
            }
        }
    }
}

if (-not $check4Fail -and $exitCode -eq 0) {
    Write-Host "  PASS: No -quit combined with -runTests." -ForegroundColor Green
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

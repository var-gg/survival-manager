using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BuildBoundaryGuardFastTests
{
    [Test]
    public void RuntimeAssemblyDefinitions_KeepMetaAndPureLayerBoundaries()
    {
        var assemblies = LoadAssemblyDefinitions("Assets").ToDictionary(definition => definition.Name, StringComparer.Ordinal);

        AssertNoEngineReference(assemblies, "SM.Core");
        AssertNoEngineReference(assemblies, "SM.Combat");
        AssertNoEngineReference(assemblies, "SM.Meta");
        AssertNoEngineReference(assemblies, "SM.Meta.Serialization");
        AssertNoEngineReference(assemblies, "SM.HeadlessMetrics");
        AssertNoEngineReference(assemblies, "SM.HeadlessPolicies");
        AssertNoEngineReference(assemblies, "SM.Persistence.Abstractions");

        AssertAssemblyReferences(assemblies, "SM.Meta", "SM.Core", "SM.Combat");
        AssertAssemblyReferences(assemblies, "SM.Meta.Serialization", "SM.Core", "SM.Combat", "SM.Meta");
        AssertAssemblyReferences(assemblies, "SM.HeadlessMetrics", "SM.Core", "SM.Combat");
        AssertAssemblyReferences(assemblies, "SM.HeadlessPolicies", "SM.Combat");
        AssertAssemblyReferences(assemblies, "SM.Persistence.Abstractions", "SM.Core", "SM.Meta");
        AssertNoReferences(
            assemblies,
            "SM.Meta.Serialization",
            "SM.Content",
            "SM.Persistence.Abstractions",
            "SM.Persistence.Json",
            "SM.Unity",
            "SM.Editor");
        AssertNoReferences(
            assemblies,
            "SM.HeadlessPolicies",
            "SM.Content",
            "SM.HeadlessMetrics",
            "SM.Meta",
            "SM.Meta.Serialization",
            "SM.Persistence.Abstractions",
            "SM.Persistence.Json",
            "SM.Unity",
            "SM.Editor");
        AssertNoReferences(
            assemblies,
            "SM.HeadlessMetrics",
            "SM.Content",
            "SM.Meta",
            "SM.Meta.Serialization",
            "SM.Persistence.Abstractions",
            "SM.Persistence.Json",
            "SM.Unity",
            "SM.Editor");
        AssertNoReferences(
            assemblies,
            "SM.Persistence.Abstractions",
            "SM.Content",
            "SM.Persistence.Json",
            "SM.Unity",
            "SM.Editor");
        Assert.That(assemblies["SM.Persistence.Json"].References, Does.Not.Contain("SM.Content"));
        Assert.That(assemblies["SM.Tests.FastUnit"].References, Does.Not.Contain("SM.Editor"));
        Assert.That(assemblies["SM.Tests.FastUnit"].References, Does.Not.Contain("Unity.Localization.Editor"));
        Assert.That(assemblies["SM.Tests.FastUnit"].References, Does.Not.Contain("SM.Tests.EditMode"));
        Assert.That(assemblies["SM.Tests.EditMode"].References, Does.Contain("SM.Tests.FastUnit"));
        Assert.That(assemblies["SM.Tests.PlayMode"].References, Does.Not.Contain("SM.Editor"));

        foreach (var productionAssembly in assemblies.Values.Where(assembly => !assembly.Name.StartsWith("SM.Tests.", StringComparison.Ordinal)))
        {
            Assert.That(
                productionAssembly.References.Where(reference => reference.StartsWith("SM.Tests.", StringComparison.Ordinal)),
                Is.Empty,
                $"{productionAssembly.Name} must not reference test assemblies.");
        }
    }

    [Test]
    public void MetaRuntimeSources_DoNotReferenceContentOrUnity()
    {
        var forbiddenTokens = new[]
        {
            "using SM.Content",
            string.Concat("using SM.Content", ".Definitions"),
            "UnityEngine",
            "UnityEditor"
        };
        var metaRoot = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Meta");
        foreach (var path in Directory.EnumerateFiles(metaRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbiddenTokens)
            {
                Assert.That(text, Does.Not.Contain(token), $"{path} must stay free of {token}.");
            }
        }
    }

    [Test]
    public void GameSessionState_FacadeFileStaysBelowBoundaryBudget()
    {
        var path = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "GameSessionState.cs");
        Assert.That(File.ReadLines(path).Count(), Is.LessThanOrEqualTo(1500));
        Assert.That(
            CountRegexMatches(ReadCodeText(path), @"^\s*public\s+(?:[\w<>?,\.]+\s+)+\w+\s*\("),
            Is.LessThanOrEqualTo(65),
            "GameSessionState public facade must not keep growing; move new flow work into focused collaborators.");
    }

    [Test]
    public void GameSessionState_PartialStructureBudget_DoesNotRegrow()
    {
        // 2026-07 준비도 감사: GameSessionState는 partial 11개(총 ~6.2k 라인) god-facade budget 상태이며,
        // Session flow partial들이 공유 back-ref 위에서 커지는 패턴은 save-load 전역 Empty 오염 사고와 동형이다.
        // budget = 현재 실측 + 여유(파일 수 +1, 최대 라인 +10%). 초과 = 세션에 더 얹지 말고 분할 백로그를 소화하라.
        var unityRoot = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity");
        var partials = Directory.EnumerateFiles(unityRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Regex.IsMatch(ReadCodeText(path), @"partial\s+class\s+GameSessionState\b"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.That(partials, Is.Not.Empty,
            "GameSessionState partial을 하나도 찾지 못했다 — 가드 자체가 죽었다(탐지 경로/선언 이동 여부를 확인하라).");
        Assert.That(partials.Count, Is.LessThanOrEqualTo(12),
            "GameSessionState partial 파일 수가 budget(12 = 실측 11 + 1)을 넘었다 — 새 partial을 더 얹지 말고 " +
            "세션 밖 focused collaborator로 책임을 분리해 분할 백로그를 소화하라.\n현재 partial:\n" +
            string.Join("\n", partials));

        var totalLines = 0;
        foreach (var path in partials)
        {
            var lineCount = File.ReadLines(path).Count();
            totalLines += lineCount;
            Assert.That(lineCount, Is.LessThanOrEqualTo(1610),
                $"{path} ({lineCount} 라인)가 partial 단일 파일 budget(1610 = 실측 최대 SessionExpeditionFlow 1467 + 10%)을 " +
                "넘었다 — 이 partial에 더 얹지 말고 분할 백로그를 소화하라(한 파일 = 한 책임).");
        }

        Assert.That(totalLines, Is.LessThanOrEqualTo(6800),
            $"GameSessionState partial 총 라인({totalLines})이 budget(6800 = 실측 ~6.2k + 10%)을 넘었다 — " +
            "facade 총량이 계속 자라면 세션이 소유할 truth인지 먼저 분류하고 분할 백로그를 소화하라.");
    }

    [Test]
    public void BattleComposition_SceneRestartStaysOnSessionSingleSource()
    {
        // 2026-07 준비도 감사: RestartSameSeed가 씬에서 BattleFactory를 직접 호출하는 2nd battle-truth라
        // 보스 overlay bootstrap·status rule fallback이 빠졌다(같은 시드 재시작 ≠ 같은 전투). 씬은 세션 합성
        // 단일 소스(TryComposeBattleState)만 소비한다 — FinalUnits id·BuildStableSeed 계열(2nd-consumer drift) 재발 차단.
        // 행동 골든은 BattleRestartSameSeedDeterminismTests(BatchOnly, byte-identical 스트림).
        var controllerPath = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "BattleScreenController.cs");
        var controllerCode = ReadCodeText(controllerPath);
        Assert.That(controllerCode, Does.Not.Contain(string.Concat("BattleFactory", ".Create")),
            $"{controllerPath} 가 BattleFactory를 직접 호출한다 — 전투 합성은 세션 단일 소스(TryComposeBattleState)를 경유하라.");
        Assert.That(controllerCode, Does.Contain("TryComposeBattleState"),
            $"{controllerPath} 재시작 경로가 세션 합성 단일 소스(TryComposeBattleState)를 소비해야 한다.");

        var resolutionPath = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "GameSessionState.BattleResolution.cs");
        var resolutionCode = ReadCodeText(resolutionPath);
        Assert.That(resolutionCode, Does.Contain("ApplyBattleBootstrap"),
            $"{resolutionPath} 합성 단일 소스에서 인카운터 bootstrap이 사라졌다 — 보스 overlay가 모든 소비자에서 침묵한다.");
    }

    [Test]
    public void SessionItemGeneration_StaysThinFacade()
    {
        var path = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "Session", "SessionItemGeneration.cs");
        var codeText = ReadCodeText(path);
        var forbiddenTokens = new[]
        {
            "ItemRarityTierValue",
            "ItemIdentityValue",
            "AffixTierValue",
            "TryGetAffixDefinition",
            "GetCanonicalAffixIds",
        };

        Assert.That(File.ReadLines(path).Count(), Is.LessThanOrEqualTo(80));
        foreach (var token in forbiddenTokens)
        {
            Assert.That(
                codeText,
                Does.Not.Contain(token),
                $"{path} must stay as a thin session facade; item/affix selection policy belongs in SessionInventoryItemBuilder.");
        }
    }

    [Test]
    public void GameSessionState_DoesNotOwnProductionNarrativeResourcesBootstrap()
    {
        var gameSessionPath = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "GameSessionState.cs");
        var providerPath = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "GameSessionRuntimeBootstrapProvider.cs");
        var productionBootstrapToken = string.Concat("NarrativeRuntimeBootstrap", ".LoadFromResources");

        Assert.That(
            ReadCodeText(gameSessionPath),
            Does.Not.Contain(productionBootstrapToken),
            "GameSessionState public facade must delegate production narrative Resources bootstrap to the runtime provider.");
        Assert.That(
            ReadCodeText(providerPath),
            Does.Contain(productionBootstrapToken),
            "GameSessionRuntimeBootstrapProvider must be the explicit production narrative Resources choke point.");
    }

    [Test]
    public void SessionRuntimeSources_DoNotOwnAssetLoadingChokePoints()
    {
        var forbiddenTokens = new[]
        {
            string.Concat("Resources", ".Load"),
            "AssetDatabase",
            "ScriptableObject",
            string.Concat("RuntimeCombat", "ContentLookup"),
            string.Concat("NarrativeRuntimeBootstrap", ".LoadFromResources"),
            "UnityEditor"
        };
        var sessionRoot = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "Session");
        foreach (var path in Directory.EnumerateFiles(sessionRoot, "*.cs", SearchOption.AllDirectories))
        {
            var codeText = ReadCodeText(path);
            foreach (var token in forbiddenTokens)
            {
                Assert.That(
                    codeText,
                    Does.Not.Contain(token),
                    $"{path} must not introduce asset/editor loading token '{token}'. Keep those in Unity content/bootstrap choke points.");
            }
        }
    }

    [Test]
    public void SessionFlowCoreDelegationBudget_DoesNotRegrow()
    {
        var sessionRoot = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "Session");
        var delegationCount = Directory.EnumerateFiles(sessionRoot, "*.cs", SearchOption.AllDirectories)
            .Select(ReadCodeText)
            .Sum(text => Regex.Matches(text, @"_session\.\w+Core\s*\(").Count);

        Assert.That(
            delegationCount,
            Is.LessThanOrEqualTo(30),
            "Session service objects should not add new _session.*Core(...) ownership-delegation wrappers.");
    }

    [Test]
    public void ContentConversionSources_StayInternalUnityAdapterBoundary()
    {
        var conversionRoot = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "Unity", "ContentConversion");
        var registryPath = Path.Combine(conversionRoot, "ContentDefinitionRegistry.cs").Replace('\\', '/');
        var assetLoadingPatterns = new[]
        {
            (Pattern: @"Resources\.Load(All)?\s*\(", Description: "Resources loading"),
            (Pattern: @"\bAssetDatabase\b", Description: "AssetDatabase"),
            (Pattern: @"^\s*using\s+UnityEditor\s*;", Description: "UnityEditor using"),
            (Pattern: @"RuntimeCombatContentFileParser", Description: "file fallback parser"),
        };
        var forbiddenAdapterPatterns = new[]
        {
            (Pattern: @"^\s*public\s+", Description: "public API surface"),
            (Pattern: @"\bSM\.Persistence\b", Description: "persistence dependency"),
            (Pattern: @"\bSaveProfile\b", Description: "save profile ownership"),
            (Pattern: @"\bGameSessionState\b", Description: "session facade ownership"),
            (Pattern: string.Concat(@"\bRuntimeCombat", @"ContentLookup\b"), Description: "production lookup construction"),
            (Pattern: @"\bMonoBehaviour\b", Description: "MonoBehaviour ownership"),
            (Pattern: @"UnityEngine\.SceneManagement", Description: "scene ownership"),
            (Pattern: @"UnityEngine\.UIElements", Description: "UIElements ownership"),
            (Pattern: @"\bUIDocument\b", Description: "UI document ownership"),
        };

        Assert.That(
            Directory.EnumerateFiles(conversionRoot, "*.asmdef", SearchOption.AllDirectories),
            Is.Empty,
            "ContentConversion is intentionally an internal SM.Unity folder boundary, not a standalone asmdef in this phase.");

        foreach (var path in Directory.EnumerateFiles(conversionRoot, "*.cs", SearchOption.AllDirectories))
        {
            var normalizedPath = path.Replace('\\', '/');
            var codeText = ReadCodeText(path);
            Assert.That(
                codeText,
                Does.Contain("namespace SM.Unity.ContentConversion;"),
                $"{path} must stay in the SM.Unity.ContentConversion namespace.");

            foreach (var rule in forbiddenAdapterPatterns)
            {
                Assert.That(
                    Regex.IsMatch(codeText, rule.Pattern, RegexOptions.Multiline),
                    Is.False,
                    $"{path} must not introduce {rule.Description}; keep ContentConversion as authored-to-runtime adapter code.");
            }

            if (string.Equals(normalizedPath, registryPath, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var rule in assetLoadingPatterns)
            {
                Assert.That(
                    Regex.IsMatch(codeText, rule.Pattern, RegexOptions.Multiline),
                    Is.False,
                    $"{path} must not introduce {rule.Description}; asset loading stays in ContentDefinitionRegistry.");
            }
        }
    }

    [Test]
    public void EditModeTestClasses_DeclareClassLevelExecutionLane()
    {
        var testRoot = Path.Combine("Assets", "Tests", "EditMode");
        foreach (var path in Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            var codeText = ReadCodeText(path);
            if (!ContainsTestAttribute(codeText))
            {
                continue;
            }

            var classMatches = Regex.Matches(
                codeText,
                @"(?<attributes>(?:\[[^\]]+\]\s*)*)public\s+(?:sealed\s+)?class\s+(?<name>\w+Tests)\b",
                RegexOptions.Singleline);

            foreach (Match classMatch in classMatches)
            {
                var attributes = classMatch.Groups["attributes"].Value;
                var className = classMatch.Groups["name"].Value;
                Assert.That(
                    Regex.IsMatch(attributes, @"\[Category\(\s*""(?:FastUnit|BatchOnly|ManualLoopD)""\s*\)\]"),
                    Is.True,
                    $"{path} test class {className} must declare a class-level FastUnit, BatchOnly, or ManualLoopD category.");
            }
        }
    }

    [Test]
    public void FastUnitCategory_StaysInDedicatedAssemblyFolder()
    {
        var testRoot = Path.Combine("Assets", "Tests", "EditMode");
        var fastUnitRoot = Path.Combine("Assets", "Tests", "EditMode", "FastUnit")
            .Replace('\\', '/');

        foreach (var path in Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            if (!IsFastUnitTest(text))
            {
                continue;
            }

            var normalizedPath = path.Replace('\\', '/');
            Assert.That(
                normalizedPath.StartsWith(fastUnitRoot, StringComparison.Ordinal),
                Is.True,
                $"{path} is FastUnit and must live under the dedicated SM.Tests.FastUnit assembly folder.");
        }
    }

    [Test]
    public void FastUnitTests_UseGameSessionFactoryInsteadOfPublicSessionConstructor()
    {
        var allowedFactoryPath = Path.Combine("Assets", "Tests", "EditMode", "FastUnit", "Fakes", "GameSessionTestFactory.cs")
            .Replace('\\', '/');
        var testRoot = Path.Combine("Assets", "Tests", "EditMode");
        foreach (var path in Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            var normalizedPath = path.Replace('\\', '/');
            if (string.Equals(normalizedPath, allowedFactoryPath, StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (!IsFastUnitTest(text))
            {
                continue;
            }

            var codeText = ReadCodeText(path);

            Assert.That(
                Regex.IsMatch(codeText, @"new\s+GameSessionState\s*\("),
                Is.False,
                $"{path} must use GameSessionTestFactory.Create(...) unless it is BatchOnly.");
        }
    }

    [Test]
    public void NonBatchOnlyEditModeTests_DoNotCallProductionContentBootstrap()
    {
        var allowedFactoryPath = Path.Combine("Assets", "Tests", "EditMode", "FastUnit", "Fakes", "GameSessionTestFactory.cs")
            .Replace('\\', '/');
        var forbiddenPatterns = new[]
        {
            (Pattern: @"Resources\.Load(All)?\s*\(", Description: "Resources loading"),
            (Pattern: @"using\s+static\s+UnityEngine\.Resources\s*;", Description: "Resources static import alias"),
            (Pattern: @"using\s+\w+\s*=\s*UnityEngine\.Resources\s*;", Description: "Resources type alias"),
            (Pattern: string.Concat(@"\bRuntimeCombat", @"ContentLookup\b"), Description: "production combat content lookup token"),
            (Pattern: string.Concat(@"using\s+\w+\s*=\s*SM\.Unity\.RuntimeCombat", @"ContentLookup\s*;"), Description: "production combat content lookup alias"),
            (Pattern: @"NarrativeRuntimeBootstrap\.LoadFromResources\s*\(", Description: "narrative resources bootstrap"),
            (Pattern: @"using\s+\w+\s*=\s*SM\.Unity\.NarrativeRuntimeBootstrap\s*;", Description: "narrative bootstrap alias"),
            (Pattern: @"\bNarrativeRuntimeBootstrap\s+\w+\s*\(", Description: "narrative bootstrap wrapper method signature")
        };
        var testRoot = Path.Combine("Assets", "Tests", "EditMode");
        foreach (var path in Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            if (IsBatchOnlyTest(text))
            {
                continue;
            }

            var normalizedPath = path.Replace('\\', '/');
            var codeText = ReadCodeText(path);
            foreach (var rule in forbiddenPatterns)
            {
                Assert.That(
                    Regex.IsMatch(codeText, rule.Pattern),
                    Is.False,
                    $"{path} must not use {rule.Description} outside BatchOnly.");
            }

            if (!string.Equals(normalizedPath, allowedFactoryPath, StringComparison.Ordinal))
            {
                Assert.That(
                    Regex.IsMatch(codeText, @"new\s+GameSessionState\s*\("),
                    Is.False,
                    $"{path} must not call the public GameSessionState constructor outside BatchOnly.");
            }
        }
    }

    [Test]
    public void FastUnitTests_DoNotUseAuthoredUnityObjectFixtures()
    {
        var forbiddenPatterns = new[]
        {
            (Pattern: string.Concat("ScriptableObject", @"\.CreateInstance"), Description: "scriptable object creation API"),
            (Pattern: string.Concat(@"UnityEngine", @"\.ScriptableObject"), Description: "Unity scriptable object token"),
            (Pattern: string.Concat(@"using\s+\w+\s*=\s*UnityEngine", @"\.ScriptableObject\s*;"), Description: "ScriptableObject alias"),
            (Pattern: string.Concat(@"UnityEngine", @"\.Object"), Description: "Unity object token"),
            (Pattern: string.Concat(@"using\s+\w+\s*=\s*UnityEngine", @"\.Object\s*;"), Description: "Unity object alias"),
            (Pattern: string.Concat(@"Object\.(?:Instantiate|Destroy|Destroy", @"Immediate)\s*\("), Description: "Unity object lifecycle API"),
            (Pattern: string.Concat(@"Destroy", @"Immediate\s*\("), Description: "immediate Unity object destruction"),
            (Pattern: string.Concat(@"using\s+SM\.Content", @"\.Definitions"), Description: "content definitions import"),
            (Pattern: string.Concat(@"\bSM\.Content", @"\.Definitions\b"), Description: "content definitions token"),
            (Pattern: string.Concat(@"Resources", @"\.Load"), Description: "resource loading call"),
            (Pattern: @"using\s+static\s+UnityEngine\.Resources\s*;", Description: "Resources static import alias"),
            (Pattern: @"using\s+\w+\s*=\s*UnityEngine\.Resources\s*;", Description: "Resources type alias"),
            (Pattern: string.Concat("NarrativeRuntimeBootstrap", @"\.LoadFromResources"), Description: "narrative resources bootstrap"),
            (Pattern: string.Concat("RuntimeCombat", @"ContentLookup"), Description: "production content lookup token"),
            (Pattern: string.Concat(@"using\s+\w+\s*=\s*SM\.Unity\.RuntimeCombat", @"ContentLookup\s*;"), Description: "production content lookup alias"),
        };
        var testRoot = Path.Combine("Assets", "Tests", "EditMode");
        foreach (var path in Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            if (!IsFastUnitTest(text))
            {
                continue;
            }

            var codeText = ReadCodeText(path);
            foreach (var rule in forbiddenPatterns)
            {
                Assert.That(
                    Regex.IsMatch(codeText, rule.Pattern),
                    Is.False,
                    $"{path} is FastUnit and must not use authored Unity object fixtures or production content bootstrap pattern '{rule.Description}'.");
            }
        }
    }

    private static void AssertAssemblyReferences(
        IReadOnlyDictionary<string, AssemblyDefinitionInfo> assemblies,
        string assemblyName,
        params string[] expectedReferences)
    {
        Assert.That(assemblies.ContainsKey(assemblyName), Is.True, $"{assemblyName} asmdef must exist.");
        Assert.That(
            assemblies[assemblyName].References,
            Is.EquivalentTo(expectedReferences),
            $"{assemblyName} references must stay exact.");
    }

    private static void AssertNoEngineReference(IReadOnlyDictionary<string, AssemblyDefinitionInfo> assemblies, string assemblyName)
    {
        Assert.That(assemblies.ContainsKey(assemblyName), Is.True, $"{assemblyName} asmdef must exist.");
        Assert.That(assemblies[assemblyName].NoEngineReferences, Is.True, $"{assemblyName} must set noEngineReferences=true.");
    }

    private static void AssertNoReferences(
        IReadOnlyDictionary<string, AssemblyDefinitionInfo> assemblies,
        string assemblyName,
        params string[] forbiddenReferences)
    {
        Assert.That(assemblies.ContainsKey(assemblyName), Is.True, $"{assemblyName} asmdef must exist.");
        foreach (var forbiddenReference in forbiddenReferences)
        {
            Assert.That(
                assemblies[assemblyName].References,
                Does.Not.Contain(forbiddenReference),
                $"{assemblyName} must not reference {forbiddenReference}.");
        }
    }

    private static bool IsFastUnitTest(string text)
    {
        return text.Contains("[Category(\"FastUnit\")]", StringComparison.Ordinal);
    }

    private static bool IsBatchOnlyTest(string text)
    {
        return text.Contains("[Category(\"BatchOnly\")]", StringComparison.Ordinal);
    }

    private static bool ContainsTestAttribute(string text)
    {
        return text.Contains("[Test]", StringComparison.Ordinal)
               || text.Contains("[TestCase", StringComparison.Ordinal)
               || text.Contains("[UnityTest]", StringComparison.Ordinal);
    }

    private static int CountRegexMatches(string text, string pattern)
    {
        return Regex.Matches(text, pattern, RegexOptions.Multiline).Count;
    }

    private static string ReadCodeText(string path)
    {
        return string.Join(
            Environment.NewLine,
            File.ReadAllLines(path)
                .Select(line => line.TrimStart())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                               && !line.StartsWith("*", StringComparison.Ordinal)
                               && !line.StartsWith("///", StringComparison.Ordinal)
                               && !line.StartsWith("/*", StringComparison.Ordinal)));
    }

    private static IEnumerable<AssemblyDefinitionInfo> LoadAssemblyDefinitions(string root)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories))
        {
            var json = File.ReadAllText(path);
            var name = MatchStringProperty(json, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            yield return new AssemblyDefinitionInfo(
                name,
                path.Replace('\\', '/'),
                MatchStringArrayProperty(json, "references"),
                MatchBoolProperty(json, "noEngineReferences"));
        }
    }

    private static string MatchStringProperty(string json, string propertyName)
    {
        var match = Regex.Match(json, $"\"{Regex.Escape(propertyName)}\"\\s*:\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static IReadOnlyList<string> MatchStringArrayProperty(string json, string propertyName)
    {
        var match = Regex.Match(json, $"\"{Regex.Escape(propertyName)}\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (!match.Success)
        {
            return Array.Empty<string>();
        }

        return Regex.Matches(match.Groups[1].Value, "\"([^\"]*)\"")
            .Select(item => item.Groups[1].Value)
            .ToArray();
    }

    private static bool MatchBoolProperty(string json, string propertyName)
    {
        var match = Regex.Match(json, $"\"{Regex.Escape(propertyName)}\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
        return match.Success && string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AssemblyDefinitionInfo(
        string Name,
        string Path,
        IReadOnlyList<string> References,
        bool NoEngineReferences);
}

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
        AssertNoEngineReference(assemblies, "SM.Persistence.Abstractions");

        Assert.That(assemblies["SM.Meta"].References, Is.EquivalentTo(new[] { "SM.Core", "SM.Combat" }));
        Assert.That(assemblies["SM.Meta.Serialization"].References, Does.Contain("SM.Meta"));
        Assert.That(assemblies["SM.Meta.Serialization"].References, Does.Not.Contain("SM.Content"));
        Assert.That(assemblies["SM.Persistence.Abstractions"].References, Does.Not.Contain("SM.Content"));
        Assert.That(assemblies["SM.Persistence.Json"].References, Does.Not.Contain("SM.Content"));
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
        Assert.That(File.ReadLines(path).Count(), Is.LessThanOrEqualTo(2500));
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
    public void FastUnitTests_UseGameSessionFactoryInsteadOfPublicSessionConstructor()
    {
        var allowedFactoryPath = Path.Combine("Assets", "Tests", "EditMode", "Fakes", "GameSessionTestFactory.cs")
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
    public void FastUnitTests_DoNotUseAuthoredUnityObjectFixtures()
    {
        var forbiddenTokens = new[]
        {
            string.Concat("ScriptableObject", ".CreateInstance"),
            string.Concat("UnityEngine", ".Object"),
            string.Concat("Destroy", "Immediate"),
            string.Concat("using SM.Content", ".Definitions"),
            string.Concat("Resources", ".Load"),
            string.Concat("RuntimeCombat", "ContentLookup"),
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
            foreach (var token in forbiddenTokens)
            {
                Assert.That(
                    codeText,
                    Does.Not.Contain(token),
                    $"{path} is FastUnit and must not use authored Unity object fixtures or production content bootstrap token '{token}'.");
            }
        }
    }

    private static void AssertNoEngineReference(IReadOnlyDictionary<string, AssemblyDefinitionInfo> assemblies, string assemblyName)
    {
        Assert.That(assemblies.ContainsKey(assemblyName), Is.True, $"{assemblyName} asmdef must exist.");
        Assert.That(assemblies[assemblyName].NoEngineReferences, Is.True, $"{assemblyName} must set noEngineReferences=true.");
    }

    private static bool IsFastUnitTest(string text)
    {
        return text.Contains("[Category(\"FastUnit\")]", StringComparison.Ordinal);
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

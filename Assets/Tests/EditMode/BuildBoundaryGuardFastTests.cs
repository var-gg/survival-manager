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
            "using SM.Content.Definitions",
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
            if (text.Contains("[Category(\"BatchOnly\")]", StringComparison.Ordinal))
            {
                continue;
            }

            var codeText = string.Join(
                Environment.NewLine,
                File.ReadAllLines(path)
                    .Select(line => line.TrimStart())
                    .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                                   && !line.StartsWith("*", StringComparison.Ordinal)
                                   && !line.StartsWith("///", StringComparison.Ordinal)
                                   && !line.StartsWith("/*", StringComparison.Ordinal)));

            Assert.That(
                Regex.IsMatch(codeText, @"new\s+GameSessionState\s*\("),
                Is.False,
                $"{path} must use GameSessionTestFactory.Create(...) unless it is BatchOnly.");
        }
    }

    private static void AssertNoEngineReference(IReadOnlyDictionary<string, AssemblyDefinitionInfo> assemblies, string assemblyName)
    {
        Assert.That(assemblies.ContainsKey(assemblyName), Is.True, $"{assemblyName} asmdef must exist.");
        Assert.That(assemblies[assemblyName].NoEngineReferences, Is.True, $"{assemblyName} must set noEngineReferences=true.");
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("FastUnit")]
public sealed class UiArtBibleConformanceFastTests
{
    private const string RegistryPath = "Assets/_Game/UI/Foundation/ArtBible/artbible-role-registry.json";
    private const string ExceptionsPath = "Assets/_Game/UI/Foundation/ArtBible/artbible-exceptions.json";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string InventoryViewPath = "Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/InventoryView.cs";
    private const string EquipmentRefitViewPath = "Assets/_Game/Scripts/Runtime/Unity/UI/Town/Preview/EquipmentRefitView.cs";
    private const string ReportDirectory = "Library/SM/Reports";
    private const string MarkdownReportPath = ReportDirectory + "/ui-artbible-conformance.md";
    private const string JsonReportPath = ReportDirectory + "/ui-artbible-conformance.json";

    [Test]
    public void ArtBibleRegistry_Declares_Phase1_Roles_And_Exceptions()
    {
        Assert.That(File.Exists(RegistryPath), Is.True, RegistryPath);
        Assert.That(File.Exists(ExceptionsPath), Is.True, ExceptionsPath);

        var registry = File.ReadAllText(RegistryPath);
        var exceptions = File.ReadAllText(ExceptionsPath);

        Assert.That(ReadStringArray(registry, "enforcedProductionPanelUss").Count, Is.GreaterThanOrEqualTo(10));
        Assert.That(registry, Does.Contain("\"monitoredProductionPanelUss\""));
        Assert.That(registry, Does.Contain("\"button.cta.primary\""));
        Assert.That(registry, Does.Contain("\"button.cta.secondary\""));
        Assert.That(registry, Does.Contain("\"chrome.l1.outer-modal\""));
        Assert.That(registry, Does.Contain("\"chrome.l2.content-panel\""));
        Assert.That(registry, Does.Contain("\"screen.recruit.candidate-card\""));
        Assert.That(registry, Does.Contain("\"screen.passive.node\""));
        Assert.That(registry, Does.Contain("\"screen.inventory.item-cell\""));
        Assert.That(registry, Does.Contain("\"item.icon.content\""));
        Assert.That(registry, Does.Contain("\"item.badge.slot\""));
        Assert.That(registry, Does.Contain("\"item.badge.weapon-family\""));
        Assert.That(registry, Does.Contain("\"item.badge.rarity\""));
        Assert.That(registry, Does.Contain("\"item.badge.identity\""));
        Assert.That(registry, Does.Contain("\"affix.row.item-detail\""));
        Assert.That(registry, Does.Contain("\"item.state.overlay\""));
        Assert.That(registry, Does.Contain("\"operation.refit.cta\""));
        Assert.That(registry, Does.Contain("\"ui_button_gold.png\""));
        Assert.That(registry, Does.Contain("\"ui_button_dark.png\""));
        Assert.That(exceptions, Does.Contain("\"exceptions\""));
    }

    [Test]
    public void ProductionTownUi_Has_No_ArtBible_Conformance_Errors()
    {
        var registry = LoadRegistry();
        var findings = Analyze(registry);
        WriteReports(findings);

        var errors = findings
            .Where(finding => finding.Severity == ArtBibleSeverity.Error)
            .ToArray();

        Assert.That(
            errors,
            Is.Empty,
            "ArtBible conformance errors were found. See " + MarkdownReportPath + Environment.NewLine + FormatFindings(errors));

        var warnings = findings
            .Where(finding => finding.Severity == ArtBibleSeverity.Warning)
            .ToArray();

        Assert.That(
            warnings,
            Is.Empty,
            "ArtBible conformance warnings were found. See " + MarkdownReportPath + Environment.NewLine + FormatFindings(warnings));
    }

    private static ArtBibleRegistry LoadRegistry()
    {
        var json = File.ReadAllText(RegistryPath);
        return new ArtBibleRegistry(
            ReadStringArray(json, "enforcedProductionPanelUss"),
            ReadStringArray(json, "monitoredProductionPanelUss"),
            ReadStringArray(json, "productionUxmlRoots"),
            ReadStringArray(json, "forbiddenInnerChromeImages"));
    }

    private static List<ArtBibleFinding> Analyze(ArtBibleRegistry registry)
    {
        var findings = new List<ArtBibleFinding>();

        AnalyzeRuntimePanelTheme(findings);
        AnalyzeEnforcedPanels(registry, findings);
        AnalyzeMonitoredPanels(registry, findings);
        AnalyzeProductionUxmlImports(registry, findings);
        AnalyzeLocalButtonSliceDuplication(registry, findings);
        AnalyzeEquipmentRoleUsage(findings);

        findings.Sort(CompareFindings);
        return findings;
    }

    private static void AnalyzeRuntimePanelTheme(List<ArtBibleFinding> findings)
    {
        if (!File.Exists(RuntimePanelThemePath))
        {
            findings.Add(Error("AB000_MISSING_RUNTIME_PANEL_THEME", RuntimePanelThemePath, "RuntimePanelTheme.uss is required for shared ArtBible material roles."));
            return;
        }

        var theme = File.ReadAllText(RuntimePanelThemePath);
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-chrome-l2", "AB010_MISSING_CHROME_ROLE", "RuntimePanelTheme must declare L2 content chrome.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-chrome-l3", "AB010_MISSING_CHROME_ROLE", "RuntimePanelTheme must declare L3 card chrome.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-chrome-l4", "AB010_MISSING_CHROME_ROLE", "RuntimePanelTheme must declare L4 element chrome.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-cta--primary", "AB011_MISSING_BUTTON_ROLE", "RuntimePanelTheme must declare primary CTA material.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-cta--secondary", "AB011_MISSING_BUTTON_ROLE", "RuntimePanelTheme must declare secondary CTA material.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-item-cell", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare equipment item cell role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-item-icon", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare equipment item icon role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-item-badge--slot", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare item slot badge role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-item-badge--family", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare item family badge role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-item-rarity--common", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare common item rarity role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-item-rarity--rare", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare rare item rarity role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-item-rarity--epic", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare epic item rarity role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-item-identity--unique", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare unique identity role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-affix-row--implicit", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare implicit affix row role.");
        RequireToken(findings, RuntimePanelThemePath, theme, ".sm-operation--refit", "AB013_MISSING_EQUIPMENT_ROLE", "RuntimePanelTheme must declare refit operation role.");
        RequireToken(findings, RuntimePanelThemePath, theme, "ui_button_gold.png", "AB012_MISSING_BUTTON_MATERIAL", "Primary CTA must use the gold sliced button material.");
        RequireToken(findings, RuntimePanelThemePath, theme, "ui_button_dark.png", "AB012_MISSING_BUTTON_MATERIAL", "Secondary CTA must use the dark sliced button material.");
        AnalyzeCanonicalButtonSlices(RuntimePanelThemePath, theme, findings, ArtBibleSeverity.Error);
    }

    private static void AnalyzeEnforcedPanels(ArtBibleRegistry registry, List<ArtBibleFinding> findings)
    {
        foreach (var path in registry.EnforcedProductionPanelUss)
        {
            if (!File.Exists(path))
            {
                findings.Add(Error("AB020_MISSING_ENFORCED_PANEL_USS", path, "The enforced production panel USS is missing."));
                continue;
            }

            var uss = File.ReadAllText(path);
            RequireToken(findings, path, uss, "ui_panel_frame_outer.png", "AB021_MISSING_OUTER_MODAL_FRAME", "Production modal panels must keep the L1 ornate outer frame.");

            foreach (var image in registry.ForbiddenInnerChromeImages)
            {
                if (uss.Contains(image, StringComparison.Ordinal))
                {
                    findings.Add(Error("AB001_FORBIDDEN_INNER_ORNATE_CHROME", path, "Enforced panel uses forbidden inner ornate chrome image: " + image));
                }
            }

            AnalyzeForbiddenEquipmentRarityClasses(path, uss, findings);
            AnalyzeCanonicalButtonSlices(path, uss, findings, ArtBibleSeverity.Error);
        }
    }

    private static void AnalyzeMonitoredPanels(ArtBibleRegistry registry, List<ArtBibleFinding> findings)
    {
        foreach (var path in registry.MonitoredProductionPanelUss)
        {
            if (!File.Exists(path))
            {
                findings.Add(Warning("AB150_MISSING_MONITORED_PANEL_USS", path, "A monitored production panel USS is missing."));
                continue;
            }

            var uss = File.ReadAllText(path);
            foreach (var image in registry.ForbiddenInnerChromeImages)
            {
                if (uss.Contains(image, StringComparison.Ordinal))
                {
                    findings.Add(Warning("AB151_MONITORED_PANEL_INNER_CHROME", path, "Monitored panel still uses inner ornate chrome image: " + image));
                }
            }

            AnalyzeForbiddenEquipmentRarityClasses(path, uss, findings);
            AnalyzeCanonicalButtonSlices(path, uss, findings, ArtBibleSeverity.Warning);
        }
    }

    private static void AnalyzeProductionUxmlImports(ArtBibleRegistry registry, List<ArtBibleFinding> findings)
    {
        foreach (var path in EnumerateFiles(registry.ProductionUxmlRoots, "*.uxml"))
        {
            var uxml = File.ReadAllText(path);
            if (uxml.Contains("Screens/Town/Preview", StringComparison.Ordinal))
            {
                findings.Add(Error("AB101_PRODUCTION_IMPORTS_PREVIEW_STYLE", path, "Production UXML must not import a Town Preview USS."));
            }
        }
    }

    private static void AnalyzeLocalButtonSliceDuplication(ArtBibleRegistry registry, List<ArtBibleFinding> findings)
    {
        foreach (var path in registry.EnforcedProductionPanelUss.Concat(registry.MonitoredProductionPanelUss))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var uss = File.ReadAllText(path);
            if (uss.Contains("ui_button_gold.png", StringComparison.Ordinal) ||
                uss.Contains("ui_button_dark.png", StringComparison.Ordinal))
            {
                findings.Add(Warning("AB102_LOCAL_BUTTON_SLICE_DUPLICATION", path, "Panel-local button slice material should migrate to shared sm-cta role classes."));
            }
        }
    }

    private static void AnalyzeEquipmentRoleUsage(List<ArtBibleFinding> findings)
    {
        RequireSourceTokens(
            findings,
            InventoryViewPath,
            new[]
            {
                "sm-item-cell",
                "sm-item-icon",
                "sm-item-badge--family",
                "sm-item-rarity",
                "sm-item-state",
                "sm-affix-row"
            });
        RequireSourceTokens(
            findings,
            EquipmentRefitViewPath,
            new[]
            {
                "sm-item-cell",
                "sm-item-icon",
                "sm-item-badge--slot",
                "sm-item-rarity",
                "sm-item-state",
                "sm-affix-row",
                "sm-operation--refit"
            });

        foreach (var path in new[] { InventoryViewPath, EquipmentRefitViewPath })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            AnalyzeForbiddenEquipmentRarityClasses(path, File.ReadAllText(path), findings);
        }
    }

    private static void RequireSourceTokens(List<ArtBibleFinding> findings, string path, IEnumerable<string> tokens)
    {
        if (!File.Exists(path))
        {
            findings.Add(Error("AB030_MISSING_EQUIPMENT_VIEW", path, "Equipment presentation source file is missing."));
            return;
        }

        var text = File.ReadAllText(path);
        foreach (var token in tokens)
        {
            RequireToken(findings, path, text, token, "AB031_MISSING_EQUIPMENT_ROLE_USAGE", "Equipment views must apply shared ArtBible role class: " + token);
        }
    }

    private static void AnalyzeForbiddenEquipmentRarityClasses(string path, string text, List<ArtBibleFinding> findings)
    {
        foreach (var forbidden in new[]
                 {
                     "sm-item-rarity--magic",
                     "sm-item-rarity--legendary",
                     "sm-item-rarity--unique",
                     "sm-item-rarity--mythic"
                 })
        {
            if (text.Contains(forbidden, StringComparison.Ordinal))
            {
                findings.Add(Error("AB032_FORBIDDEN_EQUIPMENT_RARITY_CLASS", path, "V1 equipment presentation must not declare forbidden rarity class: " + forbidden));
            }
        }
    }

    private static void AnalyzeCanonicalButtonSlices(
        string path,
        string uss,
        List<ArtBibleFinding> findings,
        ArtBibleSeverity severity)
    {
        AnalyzeCanonicalButtonSlice(path, uss, findings, severity, "ui_button_gold.png");
        AnalyzeCanonicalButtonSlice(path, uss, findings, severity, "ui_button_dark.png");
    }

    private static void AnalyzeCanonicalButtonSlice(
        string path,
        string uss,
        List<ArtBibleFinding> findings,
        ArtBibleSeverity severity,
        string image)
    {
        if (!uss.Contains(image, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var block in FindCssBlocksContaining(uss, image))
        {
            if (!block.Contains("-unity-slice-left: 96", StringComparison.Ordinal) ||
                !block.Contains("-unity-slice-right: 96", StringComparison.Ordinal) ||
                !block.Contains("-unity-slice-top: 64", StringComparison.Ordinal) ||
                !block.Contains("-unity-slice-bottom: 64", StringComparison.Ordinal) ||
                !block.Contains("-unity-slice-scale: 0.34", StringComparison.Ordinal))
            {
                findings.Add(new ArtBibleFinding(
                    severity,
                    "AB002_BUTTON_SLICE_MISMATCH",
                    path,
                    image + " must use canonical 96/96/64/64 slices at scale 0.34."));
            }
        }
    }

    private static IReadOnlyList<string> FindCssBlocksContaining(string uss, string token)
    {
        var blocks = new List<string>();
        foreach (Match match in Regex.Matches(uss, @"[^{]+\{[^}]*\}", RegexOptions.Singleline))
        {
            var block = match.Value;
            if (block.Contains(token, StringComparison.Ordinal))
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    private static void RequireToken(
        List<ArtBibleFinding> findings,
        string path,
        string text,
        string token,
        string ruleId,
        string message)
    {
        if (!text.Contains(token, StringComparison.Ordinal))
        {
            findings.Add(Error(ruleId, path, message));
        }
    }

    private static IEnumerable<string> EnumerateFiles(IEnumerable<string> roots, string searchPattern)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories))
            {
                yield return NormalizePath(path);
            }
        }
    }

    private static IReadOnlyList<string> ReadStringArray(string json, string propertyName)
    {
        var pattern = "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\\[(?<items>.*?)\\]";
        var match = Regex.Match(json, pattern, RegexOptions.Singleline);
        if (!match.Success)
        {
            return Array.Empty<string>();
        }

        return Regex.Matches(match.Groups["items"].Value, "\"(?<value>(?:\\\\.|[^\"])*)\"")
            .Cast<Match>()
            .Select(matchValue => matchValue.Groups["value"].Value.Replace("\\\"", "\"", StringComparison.Ordinal))
            .ToArray();
    }

    private static void WriteReports(IReadOnlyList<ArtBibleFinding> findings)
    {
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(MarkdownReportPath, BuildMarkdownReport(findings));
        File.WriteAllText(JsonReportPath, BuildJsonReport(findings));
    }

    private static string BuildMarkdownReport(IReadOnlyList<ArtBibleFinding> findings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# UI ArtBible Conformance");
        builder.AppendLine();
        builder.AppendLine("- errors: " + findings.Count(finding => finding.Severity == ArtBibleSeverity.Error));
        builder.AppendLine("- warnings: " + findings.Count(finding => finding.Severity == ArtBibleSeverity.Warning));
        builder.AppendLine("- report: " + findings.Count(finding => finding.Severity == ArtBibleSeverity.Report));

        foreach (var severity in new[] { ArtBibleSeverity.Error, ArtBibleSeverity.Warning, ArtBibleSeverity.Report })
        {
            builder.AppendLine();
            builder.AppendLine("## " + severity);
            var severityFindings = findings.Where(finding => finding.Severity == severity).ToArray();
            if (severityFindings.Length == 0)
            {
                builder.AppendLine();
                builder.AppendLine("None.");
                continue;
            }

            foreach (var finding in severityFindings)
            {
                builder.AppendLine();
                builder.AppendLine("- `" + finding.RuleId + "` `" + finding.Path + "` " + finding.Message);
            }
        }

        return builder.ToString();
    }

    private static string BuildJsonReport(IReadOnlyList<ArtBibleFinding> findings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"errors\": " + findings.Count(finding => finding.Severity == ArtBibleSeverity.Error) + ",");
        builder.AppendLine("  \"warnings\": " + findings.Count(finding => finding.Severity == ArtBibleSeverity.Warning) + ",");
        builder.AppendLine("  \"findings\": [");

        for (var index = 0; index < findings.Count; index++)
        {
            var finding = findings[index];
            builder.Append("    { ");
            builder.Append("\"severity\": \"").Append(finding.Severity).Append("\", ");
            builder.Append("\"ruleId\": \"").Append(EscapeJson(finding.RuleId)).Append("\", ");
            builder.Append("\"path\": \"").Append(EscapeJson(finding.Path)).Append("\", ");
            builder.Append("\"message\": \"").Append(EscapeJson(finding.Message)).Append("\"");
            builder.Append(index == findings.Count - 1 ? " }" : " },");
            builder.AppendLine();
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string FormatFindings(IEnumerable<ArtBibleFinding> findings)
    {
        return string.Join(
            Environment.NewLine,
            findings.Select(finding => finding.Severity + " " + finding.RuleId + " " + finding.Path + ": " + finding.Message));
    }

    private static int CompareFindings(ArtBibleFinding left, ArtBibleFinding right)
    {
        var severityComparison = left.Severity.CompareTo(right.Severity);
        if (severityComparison != 0)
        {
            return severityComparison;
        }

        var ruleComparison = string.Compare(left.RuleId, right.RuleId, StringComparison.Ordinal);
        return ruleComparison != 0
            ? ruleComparison
            : string.Compare(left.Path, right.Path, StringComparison.Ordinal);
    }

    private static ArtBibleFinding Error(string ruleId, string path, string message)
    {
        return new ArtBibleFinding(ArtBibleSeverity.Error, ruleId, path, message);
    }

    private static ArtBibleFinding Warning(string ruleId, string path, string message)
    {
        return new ArtBibleFinding(ArtBibleSeverity.Warning, ruleId, path, message);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string EscapeJson(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private sealed class ArtBibleRegistry
    {
        public ArtBibleRegistry(
            IReadOnlyList<string> enforcedProductionPanelUss,
            IReadOnlyList<string> monitoredProductionPanelUss,
            IReadOnlyList<string> productionUxmlRoots,
            IReadOnlyList<string> forbiddenInnerChromeImages)
        {
            EnforcedProductionPanelUss = enforcedProductionPanelUss;
            MonitoredProductionPanelUss = monitoredProductionPanelUss;
            ProductionUxmlRoots = productionUxmlRoots;
            ForbiddenInnerChromeImages = forbiddenInnerChromeImages;
        }

        public IReadOnlyList<string> EnforcedProductionPanelUss { get; }

        public IReadOnlyList<string> MonitoredProductionPanelUss { get; }

        public IReadOnlyList<string> ProductionUxmlRoots { get; }

        public IReadOnlyList<string> ForbiddenInnerChromeImages { get; }
    }

    private sealed class ArtBibleFinding
    {
        public ArtBibleFinding(ArtBibleSeverity severity, string ruleId, string path, string message)
        {
            Severity = severity;
            RuleId = ruleId;
            Path = NormalizePath(path);
            Message = message;
        }

        public ArtBibleSeverity Severity { get; }

        public string RuleId { get; }

        public string Path { get; }

        public string Message { get; }
    }

    private enum ArtBibleSeverity
    {
        Error = 0,
        Warning = 1,
        Report = 2
    }
}

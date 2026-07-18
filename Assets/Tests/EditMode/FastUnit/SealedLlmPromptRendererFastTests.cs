using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.SealedLlmBridge;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SealedLlmPromptRendererFastTests
{
    [Test]
    public void SameObservation_RendersByteIdenticalPrompt()
    {
        var observation = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var seam = new SealedDecisionSeamKey(7, SealedLlmSeamTypes.Reward, 0);
        var legal = SealedLlmPromptRenderer.LegalActionKeys(seam, observation);

        var first = Encoding.UTF8.GetBytes(
            SealedLlmPromptRenderer.Render(seam, observation, legal, Manifest()));
        var second = Encoding.UTF8.GetBytes(
            SealedLlmPromptRenderer.Render(seam, observation, legal, Manifest()));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void HostileCultures_DoNotChangePromptBytes()
    {
        var observation = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var seam = new SealedDecisionSeamKey(3, SealedLlmSeamTypes.Deployment, 0);
        var legal = SealedLlmPromptRenderer.LegalActionKeys(seam, observation);
        var expected = Encoding.UTF8.GetBytes(
            SealedLlmPromptRenderer.Render(seam, observation, legal, Manifest()));
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var cultureName in new[] { "tr-TR", "de-DE", "ja-JP" })
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                var actual = Encoding.UTF8.GetBytes(
                    SealedLlmPromptRenderer.Render(seam, observation, legal, Manifest()));
                Assert.That(actual, Is.EqualTo(expected), cultureName);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    public void PolicyAndRosterObservations_RenderEveryPublicField()
    {
        var policy = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var policySeam = new SealedDecisionSeamKey(0, SealedLlmSeamTypes.Reward, 0);
        var policyPrompt = SealedLlmPromptRenderer.Render(
            policySeam,
            policy,
            SealedLlmPromptRenderer.LegalActionKeys(policySeam, policy),
            Manifest());
        AssertEveryPublicFieldRendered<HeadlessPolicyObservation>(policyPrompt);

        var roster = new HeadlessRosterPolicyObservation(
            1701,
            "chapter-a",
            "site-a",
            12,
            policy.Roster,
            policy.Wallet,
            Array.Empty<HeadlessRecruitOfferObservation>(),
            Array.Empty<HeadlessPassiveHeroObservation>(),
            Array.Empty<HeadlessRefitItemObservation>(),
            policy.EvidenceFactIdsBySignal);
        var rosterSeam = new SealedDecisionSeamKey(1, SealedLlmSeamTypes.Recruit, 0);
        var rosterPrompt = SealedLlmPromptRenderer.Render(
            rosterSeam,
            roster,
            SealedLlmPromptRenderer.LegalActionKeys(rosterSeam, roster),
            Manifest());
        AssertEveryPublicFieldRendered<HeadlessRosterPolicyObservation>(rosterPrompt);
    }

    [Test]
    public void PublicRenderSignatures_ExposeNoSessionLookupOracleOrCensusInput()
    {
        var renderMethods = typeof(SealedLlmPromptRenderer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, "Render", StringComparison.Ordinal))
            .ToArray();
        Assert.That(renderMethods.Length, Is.EqualTo(2));
        foreach (var method in renderMethods)
        {
            Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[]
            {
                typeof(SealedDecisionSeamKey),
                method.GetParameters()[1].ParameterType,
                typeof(System.Collections.Generic.IReadOnlyList<string>),
                typeof(LlmPromptManifestV1),
            }));
            var signature = string.Join("|", method.GetParameters().Select(parameter =>
                parameter.ParameterType.FullName + ":" + parameter.Name));
            Assert.That(signature, Does.Not.Contain("Session").IgnoreCase);
            Assert.That(signature, Does.Not.Contain("Lookup").IgnoreCase);
            Assert.That(signature, Does.Not.Contain("Oracle").IgnoreCase);
            Assert.That(signature, Does.Not.Contain("Census").IgnoreCase);
        }
    }

    private static void AssertEveryPublicFieldRendered<T>(string prompt)
    {
        foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            Assert.That(prompt, Does.Contain($"\"{property.Name}\":"),
                $"{typeof(T).Name}.{property.Name} disappeared from the full observation rendering.");
        }
    }

    private static LlmPromptManifestV1 Manifest()
        => new(
            "prompt-renderer-test-v1",
            "TEST PLACEHOLDER: choose only from the rendered legal menu.",
            "TEST PLACEHOLDER: blind player; use no files, tools, or hidden knowledge.",
            "scripted-stub/test",
            "mechanism=scripted-stub");
}

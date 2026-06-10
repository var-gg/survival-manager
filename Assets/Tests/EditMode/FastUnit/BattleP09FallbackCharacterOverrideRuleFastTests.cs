using System;
using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattleP09FallbackCharacterOverrideRuleFastTests
{
#if UNITY_EDITOR
    [Test]
    public void ExtraActorWithoutOwnPreset_SkipsCharacterOverride()
    {
        // 전용 프리셋 없는 extra가 character override로 등록되면 맨손 rough variant가
        // archetype 프리셋(무장 외형)을 가린다 — 등록을 생략해야 한다.
        Assert.That(
            BattleActorPresentationCatalog.ShouldRegisterEditorP09CharacterOverride(
                "extra_kojin_gate_warden",
                new[] { "warden", "raider", "hexer" }),
            Is.False);
    }

    [Test]
    public void ExtraActorWithOwnPreset_RegistersCharacterOverride()
    {
        Assert.That(
            BattleActorPresentationCatalog.ShouldRegisterEditorP09CharacterOverride(
                "extra_kojin_gate_warden",
                new[] { "extra_kojin_gate_warden" }),
            Is.True);
    }

    [Test]
    public void NonExtraCharacterWithoutPreset_KeepsRoughVariantOverride()
    {
        Assert.That(
            BattleActorPresentationCatalog.ShouldRegisterEditorP09CharacterOverride(
                "warden",
                Array.Empty<string>()),
            Is.True);
    }

    [Test]
    public void EveryAshenGateSkirmishEnemy_IsExtraActorCoveredByRule()
    {
        // site_ashen_gate_skirmish_1_squad 구성 — 캡쳐 QA에서 맨손 회귀가 났던 4기.
        var enemyCharacterIds = new[]
        {
            "extra_kojin_gate_warden",
            "extra_solarum_border_lancer",
            "extra_solarum_sigil_scribe",
            "extra_border_reliquary_carry",
        };

        foreach (var characterId in enemyCharacterIds)
        {
            Assert.That(
                ExtraActorCharacterRegistry.TryGetProfile(characterId, out _),
                Is.True,
                characterId);
            Assert.That(
                BattleActorPresentationCatalog.ShouldRegisterEditorP09CharacterOverride(
                    characterId,
                    Array.Empty<string>()),
                Is.False,
                characterId);
        }
    }
#endif
}

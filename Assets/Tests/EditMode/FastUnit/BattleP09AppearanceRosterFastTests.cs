using System.Linq;
using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattleP09AppearanceRosterFastTests
{
    private static readonly string[] CampaignExtraActorIds =
    {
        "extra_kojin_gate_warden",
        "extra_solarum_border_lancer",
        "extra_solarum_sigil_scribe",
        "extra_border_reliquary_carry",
        "extra_wolfpine_outrider",
        "extra_wolfpine_ember_runner_cell",
        "extra_grey_fang_vanguard",
        "extra_bastion_line_guard",
        "extra_bastion_reliquary_guard",
        "extra_sunken_adjudicator_lieutenant",
        "extra_sunken_bastion_adjudicator",
        "extra_tithe_mark_bearer",
        "extra_tithe_chain_cantor",
        "extra_tithe_executioner_proxy",
        "extra_tithe_inquisitor_pureflame",
        "extra_pale_memorial_keeper",
        "extra_pale_tomb_sentinel",
        "extra_black_roll_bailiff",
        "extra_crypt_list_keeper",
        "extra_lattice_root_usher",
        "extra_lattice_echo_caretaker",
        "extra_bone_orchard_watcher",
        "extra_glass_field_cleric",
        "extra_glass_shard_bailiff",
        "extra_glass_forest_recordkeeper",
        "extra_menagerie_snare_runner",
        "extra_sample_b17_survivor",
        "extra_menagerie_keeper",
        "extra_heartforge_gate_guard",
        "extra_record_rights_marker",
        "extra_heartforge_gate_warden",
        "extra_worldscar_archive_cell",
        "extra_worldscar_rite_echo",
        "extra_worldscar_record_bailiff",
    };

    [Test]
    public void CanonicalRoster_IncludesCampaignExtraActorsForP09Fallback()
    {
        Assert.That(BattleP09AppearanceRoster.CanonicalCharacterIds, Is.SupersetOf(CampaignExtraActorIds));

        foreach (var characterId in CampaignExtraActorIds)
        {
            Assert.That(
                BattleP09AppearanceRoster.TryGetDefinedDisplayName(characterId, out var displayName),
                Is.True,
                characterId);
            Assert.That(displayName, Is.Not.Empty, characterId);
        }
    }

#if UNITY_EDITOR
    [Test]
    public void EditorRoughHairName_WrapsAcrossLongRoster()
    {
        var firstExtraSlot = BattleP09AppearanceRoster.CanonicalCharacterIds
            .Select((characterId, index) => new { characterId, index })
            .First(item => item.characterId == CampaignExtraActorIds[0])
            .index;

        Assert.That(BattleActorPresentationCatalog.ResolveEditorP09RoughHairName(0), Is.EqualTo("Hair_01"));
        Assert.That(BattleActorPresentationCatalog.ResolveEditorP09RoughHairName(9), Is.EqualTo("Hair_10"));
        Assert.That(BattleActorPresentationCatalog.ResolveEditorP09RoughHairName(10), Is.EqualTo("Hair_01"));
        Assert.That(BattleActorPresentationCatalog.ResolveEditorP09RoughHairName(firstExtraSlot), Does.Match(@"^(Hair_0[1-9]|Hair_10)$"));
    }
#endif
}

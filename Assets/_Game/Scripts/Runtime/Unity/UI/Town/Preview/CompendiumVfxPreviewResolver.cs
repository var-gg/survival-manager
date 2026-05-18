using System;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

internal sealed class CompendiumVfxPreviewResolver
{
    private readonly Func<string, string, string> _localize;
    private BattleVfxCatalog? _catalog;
    private bool _catalogResolved;

    public CompendiumVfxPreviewResolver(Func<string, string, string> localize)
    {
        _localize = localize ?? throw new ArgumentNullException(nameof(localize));
    }

    public GameObject? ResolvePrefab(
        BattleSkillSpec skill,
        BattleSkillPresentationProfile presentation,
        out string prefabLabel)
    {
        prefabLabel = _localize("ui.town.compendium.vfx.prefab_missing", "미연결");
        var catalog = ResolveCatalog();
        if (catalog == null)
        {
            return null;
        }

        var cue = new BattlePresentationCue(
            ResolvePreviewCueType(presentation),
            0,
            "compendium_preview",
            ActionType: BattleActionType.ActiveSkill);
        if (!catalog.TryResolve(cue, presentation, out var entry) || entry.Prefab == null)
        {
            return null;
        }

        prefabLabel = entry.Prefab.name;
        return entry.Prefab;
    }

    private BattleVfxCatalog? ResolveCatalog()
    {
        if (_catalogResolved)
        {
            return _catalog;
        }

        _catalog = BattleVfxCatalog.ResolveRuntimeCatalog(null);
        _catalogResolved = true;
        return _catalog;
    }

    private static BattlePresentationCueType ResolvePreviewCueType(BattleSkillPresentationProfile presentation)
    {
        return presentation.Family switch
        {
            SkillPresentationFamily.Heal => BattlePresentationCueType.ActionCommitHeal,
            SkillPresentationFamily.Shield => BattlePresentationCueType.GuardEnter,
            SkillPresentationFamily.Reposition => BattlePresentationCueType.RepositionStart,
            _ => BattlePresentationCueType.ActionCommitSkill,
        };
    }
}

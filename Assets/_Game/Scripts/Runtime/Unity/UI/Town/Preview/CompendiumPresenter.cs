using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Unity.UI;

namespace SM.Unity.UI.Town.Preview;

public sealed class CompendiumPresenter : ICompendiumActions
{
    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly ContentIconResolver _iconResolver;
    private readonly CompendiumView _view;

    private CompendiumTab _activeTab = CompendiumTab.Skills;
    private string _selectedEntryId = string.Empty;
    private int _previewPlayToken;

    public CompendiumPresenter(
        GameSessionRoot root,
        GameLocalizationController localization,
        ContentTextResolver contentText,
        ContentIconResolver iconResolver,
        CompendiumView view)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _iconResolver = iconResolver ?? throw new ArgumentNullException(nameof(iconResolver));
        _view = view ?? throw new ArgumentNullException(nameof(view));
    }

    public void Initialize()
    {
        _view.Bind(this);
        _view.BindClose(Close);
        Refresh();
    }

    public void Open()
    {
        _view.Open();
        if (_activeTab == CompendiumTab.Skills)
        {
            _previewPlayToken++;
        }

        Refresh();
    }

    public void Close()
    {
        _view.Close();
    }

    public void Refresh()
    {
        _view.Render(BuildState());
    }

    public void SelectTab(CompendiumTab tab)
    {
        if (_activeTab == tab)
        {
            return;
        }

        _activeTab = tab;
        _selectedEntryId = string.Empty;
        if (_activeTab == CompendiumTab.Skills)
        {
            _previewPlayToken++;
        }

        Refresh();
    }

    public void SelectEntry(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        _selectedEntryId = id;
        if (_activeTab == CompendiumTab.Skills)
        {
            _previewPlayToken++;
        }

        Refresh();
    }

    public void PlaySelectedPreview()
    {
        if (_activeTab != CompendiumTab.Skills)
        {
            return;
        }

        _previewPlayToken++;
        Refresh();
    }

    private CompendiumViewState BuildState()
    {
        var snapshot = _root.CombatContentLookup.Snapshot;
        var skills = BuildSkills(snapshot);
        var statuses = BuildStatuses(snapshot);
        var synergies = BuildSynergies();
        var characters = BuildCharacters(snapshot);

        return new CompendiumViewState(
            Title: Localize("ui.town.compendium.title", "시스템 도감"),
            Subtitle: Localize("ui.town.compendium.subtitle", "스킬, 상태, 시너지, 캐릭터 언락 표면"),
            CloseLabel: Localize(GameLocalizationTables.UICommon, "ui.common.close", "Close"),
            Tabs: BuildTabs(),
            ActiveTab: _activeTab,
            Skills: skills,
            Statuses: statuses,
            Synergies: synergies,
            Characters: characters,
            Detail: BuildDetail(skills, statuses, synergies, characters));
    }

    private IReadOnlyList<CompendiumTabViewState> BuildTabs()
    {
        return new[]
        {
            BuildTab(CompendiumTab.Skills, "ui.town.compendium.tab.skills", "Skills"),
            BuildTab(CompendiumTab.Status, "ui.town.compendium.tab.status", "Status"),
            BuildTab(CompendiumTab.Synergy, "ui.town.compendium.tab.synergy", "Synergy"),
            BuildTab(CompendiumTab.Characters, "ui.town.compendium.tab.characters", "Characters"),
        };
    }

    private CompendiumTabViewState BuildTab(CompendiumTab tab, string key, string fallback)
        => new(tab, Localize(key, fallback), _activeTab == tab);

    private IReadOnlyList<CompendiumSkillViewState> BuildSkills(CombatContentSnapshot snapshot)
    {
        var selectedId = ResolveSelectedId(snapshot.SkillCatalog.Keys);
        return snapshot.SkillCatalog.Values
            .OrderBy(skill => PrimaryClassTag(skill))
            .ThenBy(skill => SlotOrder(skill.SlotKind))
            .ThenBy(skill => skill.Id, StringComparer.Ordinal)
            .Select(skill => BuildSkill(skill, selectedId))
            .ToList();
    }

    private CompendiumSkillViewState BuildSkill(BattleSkillSpec skill, string selectedId)
    {
        var classTags = skill.RequiredClassTags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray()
                        ?? Array.Empty<string>();
        var statuses = skill.AppliedStatuses?.Where(status => !string.IsNullOrWhiteSpace(status.StatusId)).ToArray()
                       ?? Array.Empty<StatusApplicationSpec>();
        var presentation = skill.EffectivePresentation;

        return new CompendiumSkillViewState(
            Id: skill.Id,
            Name: _contentText.GetSkillName(skill.Id),
            Description: _contentText.GetSkillDescription(skill.Id),
            SlotLabel: FormatSlot(skill.SlotKind),
            ClassLabel: classTags.Length == 0
                ? Localize("ui.town.compendium.class.any", "Any class")
                : string.Join(", ", classTags.Select(_contentText.GetClassName)),
            DamageLabel: skill.DamageType.ToString(),
            DeliveryLabel: skill.Delivery.ToString(),
            TargetLabel: skill.TargetRule.ToString(),
            PowerLabel: FormatNumber(skill.Power),
            CooldownLabel: FormatSeconds(skill.BaseCooldownSeconds),
            StatusLabel: statuses.Length == 0
                ? Localize("ui.town.compendium.status.none", "None")
                : string.Join(", ", statuses.Select(FormatStatusApplication)),
            IconId: string.IsNullOrWhiteSpace(skill.IconId) ? "-" : skill.IconId,
            VfxHookId: string.IsNullOrWhiteSpace(skill.VfxHookId) ? "-" : skill.VfxHookId,
            VfxFamilyLabel: presentation.Family.ToString(),
            VfxSkinLabel: presentation.Skin.ToString(),
            AnimationLabel: presentation.Gesture.ToString(),
            CueSequenceLabel: presentation.CueSequence.ToString(),
            VfxPreviewStyle: ResolveVfxPreviewStyle(presentation),
            IconSprite: _iconResolver.ResolveSkill(skill.Id),
            IsSelected: string.Equals(skill.Id, selectedId, StringComparison.Ordinal));
    }

    private IReadOnlyList<CompendiumStatusViewState> BuildStatuses(CombatContentSnapshot snapshot)
    {
        var statusFamilies = snapshot.StatusFamilies ?? new Dictionary<string, StatusFamilyTemplate>(StringComparer.Ordinal);
        var selectedId = ResolveSelectedId(statusFamilies.Keys);
        return statusFamilies.Values
            .OrderBy(status => status.Group)
            .ThenBy(status => status.Id, StringComparer.Ordinal)
            .Select(status => new CompendiumStatusViewState(
                Id: status.Id,
                Name: _contentText.GetStatusName(status.Id),
                Description: _contentText.GetStatusDescription(status.Id),
                GroupLabel: status.Group.ToString(),
                RuleSummary: FormatStatusRule(status),
                VfxCueId: string.IsNullOrWhiteSpace(status.VfxCueId) ? "-" : status.VfxCueId,
                IsSelected: string.Equals(status.Id, selectedId, StringComparison.Ordinal)))
            .ToList();
    }

    private IReadOnlyList<CompendiumSynergyViewState> BuildSynergies()
    {
        var ids = _root.CombatContentLookup.GetCanonicalSynergyFamilyIds();
        var selectedId = ResolveSelectedId(ids);
        var result = new List<CompendiumSynergyViewState>(ids.Count);
        foreach (var id in ids)
        {
            if (!_root.CombatContentLookup.TryGetSynergyDefinition(id, out var synergy))
            {
                continue;
            }

            var tiers = synergy.Tiers?
                .Where(tier => tier != null)
                .OrderBy(tier => tier.Threshold)
                .Select(tier => tier.Threshold.ToString())
                .ToArray() ?? Array.Empty<string>();

            result.Add(new CompendiumSynergyViewState(
                Id: id,
                Name: _contentText.GetSynergyName(id),
                Description: _contentText.GetSynergyDescription(id),
                CountedTagLabel: string.IsNullOrWhiteSpace(synergy.CountedTagId) ? "-" : synergy.CountedTagId,
                TierSummary: tiers.Length == 0 ? "-" : string.Join(" / ", tiers),
                IsSelected: string.Equals(id, selectedId, StringComparison.Ordinal)));
        }

        return result;
    }

    private IReadOnlyList<CompendiumCharacterViewState> BuildCharacters(CombatContentSnapshot snapshot)
    {
        var characters = snapshot.Characters ?? new Dictionary<string, CharacterTemplate>(StringComparer.Ordinal);
        var unlocked = _root.SessionState.Profile.Heroes
            .SelectMany(hero => new[] { hero.CharacterId, hero.HeroId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var selectedId = ResolveSelectedId(characters.Keys);
        var index = 1;
        return characters.Values
            .OrderBy(character => character.ClassId, StringComparer.Ordinal)
            .ThenBy(character => character.Id, StringComparer.Ordinal)
            .Select(character =>
            {
                var isUnlocked = unlocked.Contains(character.Id);
                var displayName = isUnlocked
                    ? _contentText.GetCharacterName(character.Id, character.DefaultArchetypeId)
                    : string.Format(Localize("ui.town.compendium.character.locked_slot", "Slot {0:00}"), index);
                index++;
                return new CompendiumCharacterViewState(
                    Id: character.Id,
                    DisplayName: displayName,
                    ClassLabel: string.IsNullOrWhiteSpace(character.ClassId) ? "-" : _contentText.GetClassName(character.ClassId),
                    UnlockLabel: isUnlocked
                        ? Localize("ui.town.compendium.character.unlocked", "Unlocked")
                        : Localize("ui.town.compendium.character.locked", "Silhouette"),
                    IsUnlocked: isUnlocked,
                    IsSelected: string.Equals(character.Id, selectedId, StringComparison.Ordinal));
            })
            .ToList();
    }

    private CompendiumDetailViewState BuildDetail(
        IReadOnlyList<CompendiumSkillViewState> skills,
        IReadOnlyList<CompendiumStatusViewState> statuses,
        IReadOnlyList<CompendiumSynergyViewState> synergies,
        IReadOnlyList<CompendiumCharacterViewState> characters)
    {
        return _activeTab switch
        {
            CompendiumTab.Status => BuildStatusDetail(statuses.FirstOrDefault(status => status.IsSelected)),
            CompendiumTab.Synergy => BuildSynergyDetail(synergies.FirstOrDefault(synergy => synergy.IsSelected)),
            CompendiumTab.Characters => BuildCharacterDetail(characters.FirstOrDefault(character => character.IsSelected)),
            _ => BuildSkillDetail(skills.FirstOrDefault(skill => skill.IsSelected)),
        };
    }

    private CompendiumDetailViewState BuildSkillDetail(CompendiumSkillViewState? skill)
    {
        if (skill == null)
        {
            return EmptyDetail();
        }

        return new CompendiumDetailViewState(
            Title: skill.Name,
            Subtitle: $"{skill.SlotLabel} / {skill.ClassLabel}",
            Description: skill.Description,
            HookLabel: $"VFX: {skill.VfxHookId} / {skill.VfxFamilyLabel} / {skill.VfxSkinLabel}",
            IconSprite: skill.IconSprite,
            VfxPreview: BuildSkillVfxPreview(skill),
            Metrics: new[]
            {
                new CompendiumMetricViewState("Id", skill.Id),
                new CompendiumMetricViewState("IconId", skill.IconId),
                new CompendiumMetricViewState("Damage", skill.DamageLabel),
                new CompendiumMetricViewState("Delivery", skill.DeliveryLabel),
                new CompendiumMetricViewState("Target", skill.TargetLabel),
                new CompendiumMetricViewState("Power", skill.PowerLabel),
                new CompendiumMetricViewState("Cooldown", skill.CooldownLabel),
                new CompendiumMetricViewState("Status", skill.StatusLabel),
                new CompendiumMetricViewState("VFX Family", skill.VfxFamilyLabel),
                new CompendiumMetricViewState("VFX Skin", skill.VfxSkinLabel),
                new CompendiumMetricViewState("Animation", skill.AnimationLabel),
                new CompendiumMetricViewState("Cue", skill.CueSequenceLabel),
            });
    }

    private static CompendiumDetailViewState BuildStatusDetail(CompendiumStatusViewState? status)
    {
        if (status == null)
        {
            return EmptyDetail();
        }

        return new CompendiumDetailViewState(
            Title: status.Name,
            Subtitle: status.GroupLabel,
            Description: status.Description,
            HookLabel: $"VFX: {status.VfxCueId}",
            IconSprite: null,
            VfxPreview: EmptyVfxPreview(),
            Metrics: new[]
            {
                new CompendiumMetricViewState("Id", status.Id),
                new CompendiumMetricViewState("Rules", status.RuleSummary),
            });
    }

    private static CompendiumDetailViewState BuildSynergyDetail(CompendiumSynergyViewState? synergy)
    {
        if (synergy == null)
        {
            return EmptyDetail();
        }

        return new CompendiumDetailViewState(
            Title: synergy.Name,
            Subtitle: synergy.CountedTagLabel,
            Description: synergy.Description,
            HookLabel: $"Breakpoints: {synergy.TierSummary}",
            IconSprite: null,
            VfxPreview: EmptyVfxPreview(),
            Metrics: new[]
            {
                new CompendiumMetricViewState("Id", synergy.Id),
                new CompendiumMetricViewState("Tag", synergy.CountedTagLabel),
                new CompendiumMetricViewState("Tiers", synergy.TierSummary),
            });
    }

    private static CompendiumDetailViewState BuildCharacterDetail(CompendiumCharacterViewState? character)
    {
        if (character == null)
        {
            return EmptyDetail();
        }

        return new CompendiumDetailViewState(
            Title: character.DisplayName,
            Subtitle: character.ClassLabel,
            Description: character.IsUnlocked
                ? "Playable character entry. Story, portrait, and dossier fields are owned by the character content lane."
                : "Locked character slot. The compendium shows the slot without revealing story or portrait content.",
            HookLabel: character.UnlockLabel,
            IconSprite: null,
            VfxPreview: EmptyVfxPreview(),
            Metrics: new[]
            {
                new CompendiumMetricViewState("Id", character.Id),
                new CompendiumMetricViewState("Unlock", character.UnlockLabel),
            });
    }

    private static CompendiumDetailViewState EmptyDetail()
    {
        return new CompendiumDetailViewState(
            Title: "-",
            Subtitle: string.Empty,
            Description: string.Empty,
            HookLabel: string.Empty,
            IconSprite: null,
            VfxPreview: EmptyVfxPreview(),
            Metrics: Array.Empty<CompendiumMetricViewState>());
    }

    private CompendiumVfxPreviewViewState BuildSkillVfxPreview(CompendiumSkillViewState skill)
    {
        return new CompendiumVfxPreviewViewState(
            CanPreview: true,
            PlayToken: _previewPlayToken,
            Title: Localize("ui.town.compendium.vfx.preview", "VFX Preview"),
            ReplayLabel: Localize("ui.town.compendium.vfx.replay", "Replay"),
            HookId: skill.VfxHookId,
            StyleKey: skill.VfxPreviewStyle,
            Caption: $"{Localize("ui.town.compendium.vfx.caption", "Hook-based showcase preview")} / {skill.VfxFamilyLabel} / {skill.VfxSkinLabel} / {skill.CueSequenceLabel}");
    }

    private static CompendiumVfxPreviewViewState EmptyVfxPreview()
    {
        return new CompendiumVfxPreviewViewState(
            CanPreview: false,
            PlayToken: 0,
            Title: string.Empty,
            ReplayLabel: string.Empty,
            HookId: string.Empty,
            StyleKey: string.Empty,
            Caption: string.Empty);
    }

    private string ResolveSelectedId(IEnumerable<string> ids)
    {
        var ordered = ids.Where(id => !string.IsNullOrWhiteSpace(id)).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (ordered.Contains(_selectedEntryId, StringComparer.Ordinal))
        {
            return _selectedEntryId;
        }

        return ordered.FirstOrDefault() ?? string.Empty;
    }

    private string FormatStatusApplication(StatusApplicationSpec status)
    {
        var name = _contentText.GetStatusName(status.StatusId);
        return $"{name} {FormatSeconds(status.DurationSeconds)} / {FormatNumber(status.Magnitude)}";
    }

    private static string FormatStatusRule(StatusFamilyTemplate status)
    {
        var flags = new List<string>();
        if (status.IsHardControl) flags.Add("hard control");
        if (status.UsesControlDiminishing) flags.Add("diminishing");
        if (status.AffectedByTenacity) flags.Add($"tenacity {status.TenacityScale:0.##}");
        if (status.AppliesPeriodicDamage) flags.Add("periodic damage");
        if (status.IsRuleModifierOnly) flags.Add("rule modifier");
        return flags.Count == 0 ? "-" : string.Join(", ", flags);
    }

    private string Localize(string key, string fallback)
    {
        return Localize(GameLocalizationTables.UITown, key, fallback);
    }

    private string Localize(string table, string key, string fallback)
    {
        return _localization.LocalizeOrFallback(table, key, fallback);
    }

    private static string PrimaryClassTag(BattleSkillSpec skill)
    {
        return skill.RequiredClassTags?.FirstOrDefault(tag => !string.IsNullOrWhiteSpace(tag)) ?? string.Empty;
    }

    private static int SlotOrder(string slot)
    {
        return CompiledSkillSlots.Normalize(slot) switch
        {
            CompiledSkillSlots.CoreActive => 0,
            CompiledSkillSlots.UtilityActive => 1,
            CompiledSkillSlots.Passive => 2,
            CompiledSkillSlots.Support => 3,
            _ => 9,
        };
    }

    private static string FormatSlot(string slot)
    {
        return CompiledSkillSlots.Normalize(slot) switch
        {
            CompiledSkillSlots.CoreActive => "Core",
            CompiledSkillSlots.UtilityActive => "Utility",
            CompiledSkillSlots.Passive => "Passive",
            CompiledSkillSlots.Support => "Support",
            _ => slot,
        };
    }

    private static string ResolveVfxPreviewStyle(BattleSkillPresentationProfile presentation)
    {
        return presentation.Family switch
        {
            SkillPresentationFamily.Heal => "heal",
            SkillPresentationFamily.Shield => "guard",
            SkillPresentationFamily.Aura or SkillPresentationFamily.PassiveProc => "aura",
            SkillPresentationFamily.Debuff => "control",
            SkillPresentationFamily.Nova or SkillPresentationFamily.Zone or SkillPresentationFamily.Trap => "area",
            SkillPresentationFamily.Projectile or SkillPresentationFamily.Ranged => presentation.Skin == SkillPresentationSkin.Arcane || presentation.Skin == SkillPresentationSkin.EchoArcane ? "arcane" : "projectile",
            SkillPresentationFamily.Reposition => "area",
            _ => presentation.Skin == SkillPresentationSkin.Arcane || presentation.Skin == SkillPresentationSkin.EchoArcane ? "arcane" : "melee",
        };
    }

    private static string FormatNumber(float value)
    {
        return Math.Abs(value) < 0.001f ? "0" : value.ToString("0.##");
    }

    private static string FormatSeconds(float value)
    {
        return value <= 0f ? "-" : $"{value:0.##}s";
    }
}

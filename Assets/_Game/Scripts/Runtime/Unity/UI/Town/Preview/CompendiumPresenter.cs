using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Unity.UI;

namespace SM.Unity.UI.Town.Preview;

internal sealed class CompendiumPresenter : ICompendiumActions
{
    private const string AnyFilterValue = "";
    private const int InitialVisibleCharacterCount = 8;

    private readonly GameSessionRoot _root;
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly ContentIconResolver _iconResolver;
    private readonly CompendiumFilterSupport _filterSupport;
    private readonly CompendiumSkillReadoutFormatter _skillReadoutFormatter;
    private readonly CompendiumVfxPreviewResolver _vfxPreviewResolver;
    private readonly CompendiumView _view;

    private CompendiumTab _activeTab = CompendiumTab.Skills;
    private string _selectedEntryId = string.Empty;
    private string _searchText = string.Empty;
    private string _skillClassFilter = AnyFilterValue;
    private string _skillSlotFilter = AnyFilterValue;
    private string _skillVfxFamilyFilter = AnyFilterValue;
    private int _previewPlayToken;

    internal CompendiumPresenter(
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
        _filterSupport = new CompendiumFilterSupport(_contentText, Localize);
        _skillReadoutFormatter = new CompendiumSkillReadoutFormatter(Localize);
        _vfxPreviewResolver = new CompendiumVfxPreviewResolver(Localize);
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

    public void SetSearchText(string searchText)
    {
        var normalized = searchText ?? string.Empty;
        if (string.Equals(_searchText, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _searchText = normalized;
        if (_activeTab == CompendiumTab.Skills)
        {
            _previewPlayToken++;
        }

        Refresh();
    }

    public void SetSkillClassFilter(string value)
    {
        SetSkillFilter(ref _skillClassFilter, value);
    }

    public void SetSkillSlotFilter(string value)
    {
        SetSkillFilter(ref _skillSlotFilter, value);
    }

    public void SetSkillVfxFamilyFilter(string value)
    {
        SetSkillFilter(ref _skillVfxFamilyFilter, value);
    }

    private CompendiumViewState BuildState()
    {
        var snapshot = _root.CombatContentLookup.Snapshot;
        var skills = BuildSkills(snapshot);
        var statuses = BuildStatuses(snapshot);
        var synergies = BuildSynergies();
        var characters = BuildCharacters(snapshot);
        var (shownCount, totalCount) = ResolveActiveCounts(
            snapshot,
            skills.Count,
            statuses.Count,
            synergies.Count,
            characters.Count);

        return new CompendiumViewState(
            Title: Localize("ui.town.compendium.title", "시스템 도감"),
            Subtitle: Localize("ui.town.compendium.subtitle", "스킬, 상태, 시너지, 캐릭터 언락 표면"),
            CloseLabel: Localize(GameLocalizationTables.UICommon, "ui.common.close", "Close"),
            Filters: _filterSupport.BuildFilterBar(
                snapshot,
                _activeTab,
                _searchText,
                _skillClassFilter,
                _skillSlotFilter,
                _skillVfxFamilyFilter,
                shownCount,
                totalCount),
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
        var filtered = snapshot.SkillCatalog.Values
            .OrderBy(skill => PrimaryClassTag(skill))
            .ThenBy(skill => CompendiumFilterSupport.SlotOrder(skill.SlotKind))
            .ThenBy(skill => skill.Id, StringComparer.Ordinal)
            .Where(skill => _filterSupport.MatchesSkillFilters(skill, _skillClassFilter, _skillSlotFilter, _skillVfxFamilyFilter))
            .Where(skill => _filterSupport.MatchesSkillSearch(skill, _searchText))
            .ToArray();
        var selectedId = ResolveSelectedId(filtered.Select(skill => skill.Id));
        return filtered
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
        var previewPrefab = _vfxPreviewResolver.ResolvePrefab(skill, presentation, out var previewPrefabLabel);

        return new CompendiumSkillViewState(
            Id: skill.Id,
            Name: _contentText.GetSkillName(skill.Id),
            Description: _contentText.GetSkillDescription(skill.Id),
            SlotLabel: _filterSupport.FormatSlot(skill.SlotKind),
            ClassLabel: classTags.Length == 0
                ? Localize("ui.town.compendium.class.any", "전체 클래스")
                : string.Join(", ", classTags.Select(_contentText.GetClassName)),
            IntentLabel: _skillReadoutFormatter.FormatIntent(skill),
            QuickStatLabel: _skillReadoutFormatter.FormatQuickStats(skill),
            CombatLineLabel: _skillReadoutFormatter.FormatCombatLine(skill),
            DamageLabel: _skillReadoutFormatter.FormatDamage(skill.DamageType),
            DeliveryLabel: _skillReadoutFormatter.FormatDelivery(skill.Delivery),
            TargetLabel: _skillReadoutFormatter.FormatTarget(skill.TargetRule),
            PowerLabel: FormatNumber(skill.Power),
            CooldownLabel: FormatSeconds(skill.BaseCooldownSeconds),
            StatusLabel: statuses.Length == 0
                ? Localize("ui.town.compendium.status.none", "없음")
                : string.Join(", ", statuses.Select(FormatStatusApplication)),
            HasStatusPayload: statuses.Length > 0,
            IconId: string.IsNullOrWhiteSpace(skill.IconId) ? "-" : skill.IconId,
            VfxHookId: string.IsNullOrWhiteSpace(skill.VfxHookId) ? "-" : skill.VfxHookId,
            VfxFamilyLabel: presentation.Family.ToString(),
            VfxSkinLabel: presentation.Skin.ToString(),
            AnimationLabel: presentation.Gesture.ToString(),
            CueSequenceLabel: presentation.CueSequence.ToString(),
            VfxPrefabLabel: previewPrefabLabel,
            VfxPreviewStyle: ResolveVfxPreviewStyle(presentation),
            VfxPreviewPrefab: previewPrefab,
            IconSprite: _iconResolver.ResolveSkill(skill.Id),
            IsSelected: string.Equals(skill.Id, selectedId, StringComparison.Ordinal));
    }

    private IReadOnlyList<CompendiumStatusViewState> BuildStatuses(CombatContentSnapshot snapshot)
    {
        var statusFamilies = snapshot.StatusFamilies ?? new Dictionary<string, StatusFamilyTemplate>(StringComparer.Ordinal);
        var filtered = statusFamilies.Values
            .OrderBy(status => status.Group)
            .ThenBy(status => status.Id, StringComparer.Ordinal)
            .Where(status => _filterSupport.MatchesStatusSearch(status, _searchText))
            .ToArray();
        var selectedId = ResolveSelectedId(filtered.Select(status => status.Id));
        return filtered
            .Select(status => new CompendiumStatusViewState(
                Id: status.Id,
                Name: _contentText.GetStatusName(status.Id),
                Description: _contentText.GetStatusDescription(status.Id),
                GroupLabel: status.Group.ToString(),
                RuleSummary: _filterSupport.FormatStatusRule(status),
                VfxCueId: string.IsNullOrWhiteSpace(status.VfxCueId) ? "-" : status.VfxCueId,
                IsSelected: string.Equals(status.Id, selectedId, StringComparison.Ordinal)))
            .ToList();
    }

    private IReadOnlyList<CompendiumSynergyViewState> BuildSynergies()
    {
        var ids = _root.CombatContentLookup.GetCanonicalSynergyFamilyIds();
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
                IsSelected: false));
        }

        var filtered = result
            .Where(synergy => _filterSupport.MatchesSynergySearch(synergy, _searchText))
            .ToArray();
        var selectedId = ResolveSelectedId(filtered.Select(synergy => synergy.Id));
        return filtered
            .Select(synergy => synergy with { IsSelected = string.Equals(synergy.Id, selectedId, StringComparison.Ordinal) })
            .ToList();
    }

    private IReadOnlyList<CompendiumCharacterViewState> BuildCharacters(CombatContentSnapshot snapshot)
    {
        var characters = snapshot.Characters ?? new Dictionary<string, CharacterTemplate>(StringComparer.Ordinal);
        var unlocked = _root.SessionState.Profile.Heroes
            .SelectMany(hero => new[] { hero.CharacterId, hero.HeroId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var index = 1;
        var result = characters.Values
            .OrderBy(character => character.ClassId, StringComparer.Ordinal)
            .ThenBy(character => character.Id, StringComparer.Ordinal)
            .Select(character =>
            {
                var slotIndex = index;
                var isProfileUnlocked = unlocked.Contains(character.Id);
                var isVisible = isProfileUnlocked || slotIndex <= InitialVisibleCharacterCount;
                var displayName = isVisible
                    ? _contentText.GetCharacterName(character.Id, character.DefaultArchetypeId)
                    : string.Format(Localize("ui.town.compendium.character.locked_slot", "Slot {0:00}"), slotIndex);
                var raceLabel = string.IsNullOrWhiteSpace(character.RaceId) ? "-" : _contentText.GetRaceName(character.RaceId);
                var classLabel = string.IsNullOrWhiteSpace(character.ClassId) ? "-" : _contentText.GetClassName(character.ClassId);
                var roleLabel = string.IsNullOrWhiteSpace(character.DefaultRoleInstructionId)
                    ? "-"
                    : _contentText.GetRoleName(character.DefaultRoleInstructionId);
                index++;
                return new CompendiumCharacterViewState(
                    Id: character.Id,
                    DisplayName: displayName,
                    Description: isVisible
                        ? _contentText.GetCharacterDescription(character.Id, character.DefaultArchetypeId)
                        : Localize("ui.town.compendium.character.locked_description", "아직 정체와 이야기가 공개되지 않은 캐릭터 슬롯입니다."),
                    RaceLabel: isVisible ? raceLabel : "-",
                    ClassLabel: isVisible ? classLabel : Localize("ui.town.compendium.character.unknown_class", "미공개"),
                    RoleLabel: isVisible ? roleLabel : "-",
                    UnlockLabel: isProfileUnlocked
                        ? Localize("ui.town.compendium.character.unlocked", "Unlocked")
                        : isVisible
                            ? Localize("ui.town.compendium.character.initial_reveal", "Initial reveal")
                        : Localize("ui.town.compendium.character.locked", "Silhouette"),
                    PortraitSprite: isVisible ? _iconResolver.ResolveCharacter(character.Id) : null,
                    IsUnlocked: isVisible,
                    IsSelected: false);
            })
            .Where(character => _filterSupport.MatchesCharacterSearch(character, _searchText))
            .ToArray();
        var selectedId = ResolveSelectedId(result.Select(character => character.Id));
        return result
            .Select(character => character with { IsSelected = string.Equals(character.Id, selectedId, StringComparison.Ordinal) })
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
            HookLabel: $"{Localize("ui.town.compendium.hook.vfx", "연출")}: {skill.VfxHookId} / {skill.VfxFamilyLabel} / {skill.VfxSkinLabel}",
            IconSprite: skill.IconSprite,
            VfxPreview: BuildSkillVfxPreview(skill),
            Metrics: new[]
            {
                new CompendiumMetricViewState(_filterSupport.MetricLabel("id", "ID"), skill.Id),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("icon", "아이콘"), skill.IconId),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("damage", "피해"), skill.DamageLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("delivery", "전달"), skill.DeliveryLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("target", "대상"), skill.TargetLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("power", "위력"), skill.PowerLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("cooldown", "재사용"), skill.CooldownLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("status", "상태"), skill.StatusLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("vfx_family", "연출 계열"), skill.VfxFamilyLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("vfx_skin", "연출 톤"), skill.VfxSkinLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("animation", "동작"), skill.AnimationLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("cue", "큐"), skill.CueSequenceLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("prefab", "프리팹"), skill.VfxPrefabLabel),
            });
    }

    private CompendiumDetailViewState BuildStatusDetail(CompendiumStatusViewState? status)
    {
        if (status == null)
        {
            return EmptyDetail();
        }

        return new CompendiumDetailViewState(
            Title: status.Name,
            Subtitle: status.GroupLabel,
            Description: status.Description,
            HookLabel: $"{Localize("ui.town.compendium.hook.vfx", "연출")}: {status.VfxCueId}",
            IconSprite: null,
            VfxPreview: EmptyVfxPreview(),
            Metrics: new[]
            {
                new CompendiumMetricViewState(_filterSupport.MetricLabel("id", "ID"), status.Id),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("rules", "규칙"), status.RuleSummary),
            });
    }

    private CompendiumDetailViewState BuildSynergyDetail(CompendiumSynergyViewState? synergy)
    {
        if (synergy == null)
        {
            return EmptyDetail();
        }

        return new CompendiumDetailViewState(
            Title: synergy.Name,
            Subtitle: synergy.CountedTagLabel,
            Description: synergy.Description,
            HookLabel: $"{Localize("ui.town.compendium.hook.breakpoints", "구간")}: {synergy.TierSummary}",
            IconSprite: null,
            VfxPreview: EmptyVfxPreview(),
            Metrics: new[]
            {
                new CompendiumMetricViewState(_filterSupport.MetricLabel("id", "ID"), synergy.Id),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("tag", "태그"), synergy.CountedTagLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("tiers", "단계"), synergy.TierSummary),
            });
    }

    private CompendiumDetailViewState BuildCharacterDetail(CompendiumCharacterViewState? character)
    {
        if (character == null)
        {
            return EmptyDetail();
        }

        if (!character.IsUnlocked)
        {
            return new CompendiumDetailViewState(
                Title: character.DisplayName,
                Subtitle: character.UnlockLabel,
                Description: character.Description,
                HookLabel: character.UnlockLabel,
                IconSprite: null,
                VfxPreview: EmptyVfxPreview(),
                Metrics: new[]
                {
                    new CompendiumMetricViewState(_filterSupport.MetricLabel("unlock", "공개"), character.UnlockLabel),
                    new CompendiumMetricViewState(_filterSupport.MetricLabel("race", "종족"), character.RaceLabel),
                    new CompendiumMetricViewState(_filterSupport.MetricLabel("class", "클래스"), character.ClassLabel),
                    new CompendiumMetricViewState(_filterSupport.MetricLabel("role", "역할"), character.RoleLabel),
                });
        }

        return new CompendiumDetailViewState(
            Title: character.DisplayName,
            Subtitle: $"{character.RaceLabel} / {character.ClassLabel}",
            Description: character.Description,
            HookLabel: character.UnlockLabel,
            IconSprite: character.PortraitSprite,
            VfxPreview: EmptyVfxPreview(),
            Metrics: new[]
            {
                new CompendiumMetricViewState(_filterSupport.MetricLabel("id", "ID"), character.Id),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("race", "종족"), character.RaceLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("class", "클래스"), character.ClassLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("role", "역할"), character.RoleLabel),
                new CompendiumMetricViewState(_filterSupport.MetricLabel("unlock", "공개"), character.UnlockLabel),
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
            Prefab: skill.VfxPreviewPrefab,
            PrefabLabel: skill.VfxPrefabLabel,
            PatternLabel: $"{skill.VfxFamilyLabel} / {skill.VfxSkinLabel}",
            RouteLabel: $"{skill.DeliveryLabel} -> {skill.TargetLabel}",
            AssetLabel: skill.VfxPrefabLabel,
            CasterLabel: Localize("ui.town.compendium.vfx.caster", "시전자"),
            TargetLabel: Localize("ui.town.compendium.vfx.target", "대상"),
            Caption: $"{Localize("ui.town.compendium.vfx.caption", "스킬 연출 미리보기")} / {skill.CueSequenceLabel}");
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
            Prefab: null,
            PrefabLabel: string.Empty,
            PatternLabel: string.Empty,
            RouteLabel: string.Empty,
            AssetLabel: string.Empty,
            CasterLabel: string.Empty,
            TargetLabel: string.Empty,
            Caption: string.Empty);
    }

    private void SetSkillFilter(ref string current, string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? AnyFilterValue : value;
        if (string.Equals(current, normalized, StringComparison.Ordinal))
        {
            return;
        }

        current = normalized;
        _previewPlayToken++;
        Refresh();
    }

    private (int Shown, int Total) ResolveActiveCounts(
        CombatContentSnapshot snapshot,
        int skillShown,
        int statusShown,
        int synergyShown,
        int characterShown)
    {
        return _activeTab switch
        {
            CompendiumTab.Status => (statusShown, (snapshot.StatusFamilies ?? new Dictionary<string, StatusFamilyTemplate>(StringComparer.Ordinal)).Count),
            CompendiumTab.Synergy => (synergyShown, _root.CombatContentLookup.GetCanonicalSynergyFamilyIds().Count),
            CompendiumTab.Characters => (characterShown, (snapshot.Characters ?? new Dictionary<string, CharacterTemplate>(StringComparer.Ordinal)).Count),
            _ => (skillShown, snapshot.SkillCatalog.Count),
        };
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

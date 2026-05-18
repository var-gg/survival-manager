using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Unity;
using SM.Unity.UI;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Compendium 미리보기 — real GameSessionRoot 기반으로 스킬/상태/시너지/캐릭터 도감을 연다.
/// 시안 ↔ 구현 왕복을 위해 TownPreviewCaptureUtility의 offscreen capture surface로도 사용한다.
/// </summary>
public sealed class CompendiumPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/CompendiumPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private CompendiumView _view;
    private CompendiumPresenter _presenter;

    [MenuItem("SM/Town/Compendium 미리보기", false, 18)]
    public static void Open()
    {
        var window = GetWindow<CompendiumPreviewBootstrap>("Compendium 미리보기");
        window.minSize = new Vector2(1400f, 820f);
    }

    private void CreateGUI() => BuildInto(rootVisualElement);

    /// <summary>EditorWindow + TownPreviewCaptureUtility 공용 — 지정 root에 surface preview 빌드.</summary>
    public void BuildInto(VisualElement root)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (visualTree == null)
        {
            root.Add(new Label($"UXML 못 찾음: {VisualTreePath}"));
            return;
        }

        var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);
        if (tokens != null) root.styleSheets.Add(tokens);
        if (theme != null) root.styleSheets.Add(theme);

        var container = visualTree.Instantiate();
        container.style.flexGrow = 1f;
        container.style.width = Length.Percent(100f);
        container.style.height = Length.Percent(100f);
        root.Add(container);

        _view = new CompendiumView(root);

        if (!TryWireRealSession(_view))
        {
            _view.Render(BuildSnapshotFallbackViewState());
            _view.Open();
        }
    }

    private bool TryWireRealSession(CompendiumView view)
    {
        try
        {
            var sessionRoot = PreviewSessionContext.EnsureSession();
            var contentText = PreviewSessionContext.CreateContentText(sessionRoot);
            var iconResolver = new ContentIconResolver(sessionRoot.CombatContentLookup);
            _presenter = new CompendiumPresenter(
                sessionRoot,
                sessionRoot.Localization,
                contentText,
                iconResolver,
                view);
            _presenter.Initialize();
            _presenter.Open();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CompendiumPreview] real-session wire 실패: {e.Message}");
            _presenter = null;
            return false;
        }
    }

    private static CompendiumViewState BuildSnapshotFallbackViewState()
    {
        GameObject localizationHost = null;
        try
        {
            var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
            var snapshot = lookup.Snapshot;
            localizationHost = new GameObject("__CompendiumPreviewLocalization")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var localization = localizationHost.AddComponent<GameLocalizationController>();
            var contentText = new ContentTextResolver(localization, lookup);
            var iconResolver = new ContentIconResolver(lookup);
            var readout = new CompendiumSkillReadoutFormatter(LocalizeFallback);
            var vfxResolver = new CompendiumVfxPreviewResolver(LocalizeFallback);
            var skills = snapshot.SkillCatalog.Values
                .OrderBy(skill => PrimaryClassTag(skill))
                .ThenBy(skill => skill.SlotKind.ToString(), StringComparer.Ordinal)
                .ThenBy(skill => skill.Id, StringComparer.Ordinal)
                .Take(12)
                .Select(skill => BuildSkillFallback(skill, contentText, iconResolver, readout, vfxResolver))
                .ToArray();
            var selected = skills.FirstOrDefault();

            return new CompendiumViewState(
                Title: "시스템 도감",
                Subtitle: "스킬, 상태, 시너지, 캐릭터 언락 표면",
                CloseLabel: "Close",
                Filters: BuildSkillFilters(snapshot.SkillCatalog.Count, skills.Length),
                Tabs: new[]
                {
                    new CompendiumTabViewState(CompendiumTab.Skills, "Skills", true),
                    new CompendiumTabViewState(CompendiumTab.Status, "Status", false),
                    new CompendiumTabViewState(CompendiumTab.Synergy, "Synergy", false),
                    new CompendiumTabViewState(CompendiumTab.Characters, "Characters", false),
                },
                ActiveTab: CompendiumTab.Skills,
                Skills: skills,
                Statuses: Array.Empty<CompendiumStatusViewState>(),
                Synergies: Array.Empty<CompendiumSynergyViewState>(),
                Characters: Array.Empty<CompendiumCharacterViewState>(),
                Detail: selected != null
                    ? BuildSkillDetailFallback(selected)
                    : EmptyDetailFallback());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CompendiumPreview] snapshot fallback 실패: {e.Message}");
            return BuildStaticFallbackViewState();
        }
        finally
        {
            if (localizationHost != null)
            {
                DestroyImmediate(localizationHost);
            }
        }
    }

    private static CompendiumSkillViewState BuildSkillFallback(
        BattleSkillSpec skill,
        ContentTextResolver contentText,
        ContentIconResolver iconResolver,
        CompendiumSkillReadoutFormatter readout,
        CompendiumVfxPreviewResolver vfxResolver)
    {
        var classTags = skill.RequiredClassTags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray()
                        ?? Array.Empty<string>();
        var statuses = skill.AppliedStatuses?.Where(status => !string.IsNullOrWhiteSpace(status.StatusId)).ToArray()
                       ?? Array.Empty<StatusApplicationSpec>();
        var presentation = skill.EffectivePresentation;
        var previewPrefab = vfxResolver.ResolvePrefab(skill, presentation, out var previewPrefabLabel);

        return new CompendiumSkillViewState(
            Id: skill.Id,
            Name: contentText.GetSkillName(skill.Id),
            Description: contentText.GetSkillDescription(skill.Id),
            SlotLabel: FormatSlot(skill.SlotKind),
            ClassLabel: classTags.Length == 0 ? "전체 클래스" : string.Join(", ", classTags.Select(contentText.GetClassName)),
            IntentLabel: readout.FormatIntent(skill),
            QuickStatLabel: readout.FormatQuickStats(skill),
            CombatLineLabel: readout.FormatCombatLine(skill),
            DamageLabel: readout.FormatDamage(skill.DamageType),
            DeliveryLabel: readout.FormatDelivery(skill.Delivery),
            TargetLabel: readout.FormatTarget(skill.TargetRule),
            PowerLabel: FormatNumber(skill.Power),
            CooldownLabel: FormatSeconds(skill.BaseCooldownSeconds),
            StatusLabel: statuses.Length == 0 ? "없음" : string.Join(", ", statuses.Select(status => contentText.GetStatusName(status.StatusId))),
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
            IconSprite: iconResolver.ResolveSkill(skill.Id),
            IsSelected: false);
    }

    private static CompendiumFilterBarViewState BuildSkillFilters(int totalCount, int shownCount)
    {
        var any = new[] { new CompendiumFilterOptionViewState(string.Empty, "전체") };
        return new CompendiumFilterBarViewState(
            ShowSkillFilters: true,
            SearchText: string.Empty,
            SearchPlaceholder: "검색",
            ClassLabel: "클래스",
            ClassValue: string.Empty,
            ClassOptions: any,
            SlotLabel: "슬롯",
            SlotValue: string.Empty,
            SlotOptions: any,
            VfxFamilyLabel: "연출",
            VfxFamilyValue: string.Empty,
            VfxFamilyOptions: any,
            ResultSummary: $"{shownCount}/{totalCount}");
    }

    private static CompendiumDetailViewState BuildSkillDetailFallback(CompendiumSkillViewState skill)
    {
        return new CompendiumDetailViewState(
            Title: skill.Name,
            Subtitle: $"{skill.SlotLabel} / {skill.ClassLabel}",
            Description: skill.Description,
            HookLabel: $"연출: {skill.VfxHookId} / {skill.VfxFamilyLabel} / {skill.VfxSkinLabel}",
            IconSprite: skill.IconSprite,
            VfxPreview: new CompendiumVfxPreviewViewState(
                CanPreview: true,
                PlayToken: 1,
                Title: "VFX Preview",
                ReplayLabel: "Replay",
                HookId: skill.VfxHookId,
                StyleKey: skill.VfxPreviewStyle,
                Prefab: skill.VfxPreviewPrefab,
                PrefabLabel: skill.VfxPrefabLabel,
                PatternLabel: $"{skill.VfxFamilyLabel} / {skill.VfxSkinLabel}",
                RouteLabel: $"{skill.DeliveryLabel} -> {skill.TargetLabel}",
                AssetLabel: skill.VfxPrefabLabel,
                CasterLabel: "시전자",
                TargetLabel: "대상",
                Caption: $"스킬 연출 미리보기 / {skill.CueSequenceLabel}"),
            Metrics: new[]
            {
                new CompendiumMetricViewState("ID", skill.Id),
                new CompendiumMetricViewState("아이콘", skill.IconId),
                new CompendiumMetricViewState("피해", skill.DamageLabel),
                new CompendiumMetricViewState("전달", skill.DeliveryLabel),
                new CompendiumMetricViewState("대상", skill.TargetLabel),
                new CompendiumMetricViewState("위력", skill.PowerLabel),
                new CompendiumMetricViewState("상태", skill.StatusLabel),
                new CompendiumMetricViewState("프리팹", skill.VfxPrefabLabel),
            });
    }

    private static CompendiumViewState BuildStaticFallbackViewState()
    {
        var skills = new[]
        {
            new CompendiumSkillViewState(
                Id: "skill_preview",
                Name: "도감 미리보기",
                Description: "실제 content snapshot을 읽지 못했을 때 사용하는 preview fallback입니다.",
                SlotLabel: "Active",
                ClassLabel: "전체 클래스",
                IntentLabel: "화력",
                QuickStatLabel: "위력 100 / 8s",
                CombatLineLabel: "물리 / 근접 / 가까운 적",
                DamageLabel: "물리",
                DeliveryLabel: "근접",
                TargetLabel: "가까운 적",
                PowerLabel: "100",
                CooldownLabel: "8s",
                StatusLabel: "없음",
                HasStatusPayload: false,
                IconId: "-",
                VfxHookId: "preview",
                VfxFamilyLabel: "Melee",
                VfxSkinLabel: "Steel",
                AnimationLabel: "Slash",
                CueSequenceLabel: "Impact",
                VfxPrefabLabel: "미연결",
                VfxPreviewStyle: "melee",
                VfxPreviewPrefab: null,
                IconSprite: null,
                IsSelected: true),
        };
        return new CompendiumViewState(
            Title: "시스템 도감",
            Subtitle: "preview fallback",
            CloseLabel: "Close",
            Filters: BuildSkillFilters(1, 1),
            Tabs: new[] { new CompendiumTabViewState(CompendiumTab.Skills, "Skills", true) },
            ActiveTab: CompendiumTab.Skills,
            Skills: skills,
            Statuses: Array.Empty<CompendiumStatusViewState>(),
            Synergies: Array.Empty<CompendiumSynergyViewState>(),
            Characters: Array.Empty<CompendiumCharacterViewState>(),
            Detail: BuildSkillDetailFallback(skills[0]));
    }

    private static CompendiumDetailViewState EmptyDetailFallback()
        => new(
            Title: "-",
            Subtitle: string.Empty,
            Description: string.Empty,
            HookLabel: string.Empty,
            IconSprite: null,
            VfxPreview: new CompendiumVfxPreviewViewState(false, 0, string.Empty, string.Empty, string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            Metrics: Array.Empty<CompendiumMetricViewState>());

    private static string PrimaryClassTag(BattleSkillSpec skill)
        => skill.RequiredClassTags?.FirstOrDefault(tag => !string.IsNullOrWhiteSpace(tag)) ?? string.Empty;

    private static string FormatSlot(string slot)
        => CompiledSkillSlots.Normalize(slot) switch
        {
            CompiledSkillSlots.CoreActive => "핵심",
            CompiledSkillSlots.UtilityActive => "전술",
            CompiledSkillSlots.Passive => "지속",
            CompiledSkillSlots.Support => "보조",
            _ => "Active",
        };

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
        => Math.Abs(value) < 0.001f ? "0" : value.ToString("0.##");

    private static string FormatSeconds(float value)
        => value <= 0f ? "-" : $"{value:0.##}s";

    private static string LocalizeFallback(string key, string fallback) => fallback;
}

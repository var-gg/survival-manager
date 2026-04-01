using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.RecruitmentSandbox;

public sealed class RecruitmentSandboxWindow : EditorWindow
{
    private const string MenuPath = "SM/Authoring/Recruitment Sandbox";

    private RecruitmentSandboxState _state = null!;
    private RuntimeCombatContentLookup _lookup = null!;
    private GameSessionState _session = null!;
    private RecruitPhaseState _previewPhaseState = null!;
    private RecruitPityState _previewPityState = null!;
    private int _previewGeneration;
    private Vector2 _scroll;

    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        var window = GetWindow<RecruitmentSandboxWindow>();
        window.titleContent = new GUIContent("Recruitment Sandbox");
        window.minSize = new Vector2(540f, 680f);
        window.Show();
    }

    private void OnEnable()
    {
        _state = CreateInstance<RecruitmentSandboxState>();
        _state.hideFlags = HideFlags.HideAndDontSave;
        ResetRuntimeSession();
        ResetPreviewState();
    }

    private void OnDisable()
    {
        if (_state != null)
        {
            DestroyImmediate(_state);
        }
    }

    private void OnGUI()
    {
        if (_state == null)
        {
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSection("Runtime Town Session", DrawRuntimeSessionSection);
        DrawSection("Recruit Pack Inspection", DrawPackInspectionSection);
        DrawSection("Retrain Sandbox", DrawRetrainSection);
        DrawSection("Duplicate Conversion Sandbox", DrawDuplicateSection);
        DrawSection("Dismiss Refund Sandbox", DrawDismissSection);

        EditorGUILayout.EndScrollView();
    }

    private void DrawRuntimeSessionSection()
    {
        EditorGUILayout.HelpBox(
            $"Wallet  Gold {_session.Profile.Currencies.Gold} / Echo {_session.Profile.Currencies.Echo}\n" +
            $"Refresh  free {_session.RecruitPhase.FreeRefreshesRemaining} / paid {_session.RecruitPhase.PaidRefreshCountThisPhase} / next {_session.CurrentRecruitRefreshCost} Gold\n" +
            $"Pity  Rare {_session.RecruitPity.PacksSinceRarePlusSeen}/3  Epic {_session.RecruitPity.PacksSinceEpicSeen}/8\n" +
            $"Scout  used={_session.RecruitPhase.ScoutUsedThisPhase} pending={FormatDirective(_session.RecruitPhase.PendingScoutDirective)}\n" +
            $"Plan  {FormatPlan(_session.CurrentTeamPlan)}",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reset Runtime Session"))
            {
                ResetRuntimeSession();
            }

            if (GUILayout.Button("Refresh"))
            {
                var result = _session.RerollRecruitOffers();
                _state.LastRuntimeReport = result.IsSuccess
                    ? $"Refresh 성공\nnext cost={_session.CurrentRecruitRefreshCost}\nphase paid={_session.RecruitPhase.PaidRefreshCountThisPhase}"
                    : $"Refresh 실패\n{result.Error}";
            }
        }

        _state.RuntimeScoutDirectiveKind = (ScoutDirectiveKind)EditorGUILayout.EnumPopup("Scout Directive", _state.RuntimeScoutDirectiveKind);
        if (_state.RuntimeScoutDirectiveKind == ScoutDirectiveKind.SynergyTag)
        {
            _state.RuntimeScoutSynergyTagId = EditorGUILayout.TextField("Scout Synergy Tag", _state.RuntimeScoutSynergyTagId);
        }

        if (GUILayout.Button("Use Scout"))
        {
            var result = _session.UseScout(new ScoutDirective
            {
                Kind = _state.RuntimeScoutDirectiveKind,
                SynergyTagId = _state.RuntimeScoutSynergyTagId ?? string.Empty,
            });
            _state.LastRuntimeReport = result.IsSuccess
                ? $"Scout 성공\npending={FormatDirective(_session.RecruitPhase.PendingScoutDirective)}"
                : $"Scout 실패\n{result.Error}";
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Current Recruit Offers", EditorStyles.boldLabel);
        var offers = _session.RecruitOffers;
        for (var i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"[{offer.Metadata.SlotType}] {offer.UnitBlueprintId}  tier={offer.Metadata.Tier}  fit={offer.Metadata.PlanFit}  cost={offer.Metadata.GoldCost}");
                EditorGUILayout.LabelField(
                    $"flexA={offer.FlexActiveId}  flexP={offer.FlexPassiveId}  pity={offer.Metadata.ProtectedByPity}  scout={offer.Metadata.BiasedByScout}");
                EditorGUILayout.LabelField(
                    $"score total={offer.Metadata.PlanScore.Total}  breakpoint={offer.Metadata.PlanScore.BreakpointProgressScore}  native={offer.Metadata.PlanScore.NativeTagMatchScore}  role={offer.Metadata.PlanScore.RoleNeedScore}  augment={offer.Metadata.PlanScore.AugmentHookScore}  scout={offer.Metadata.PlanScore.ScoutDirectiveScore}  over={offer.Metadata.PlanScore.OversaturationPenalty}");

                if (GUILayout.Button($"Recruit Slot {i + 1}"))
                {
                    var result = _session.Recruit(i);
                    _state.LastRuntimeReport = result.IsSuccess
                        ? $"Recruit 성공\nhero count={_session.Profile.Heroes.Count}\nwallet Gold={_session.Profile.Currencies.Gold} Echo={_session.Profile.Currencies.Echo}"
                        : $"Recruit 실패\n{result.Error}";
                    break;
                }
            }
        }

        _state.LastRuntimeReport = EditorGUILayout.TextArea(_state.LastRuntimeReport, GUILayout.MinHeight(84f));
    }

    private void DrawPackInspectionSection()
    {
        _state.PreviewSeed = EditorGUILayout.IntField("Seed", _state.PreviewSeed);
        _state.PreviewRosterArchetypeIdsCsv = EditorGUILayout.TextField("Roster CSV", _state.PreviewRosterArchetypeIdsCsv);
        _state.PreviewTemporaryAugmentIdsCsv = EditorGUILayout.TextField("Temp Augments CSV", _state.PreviewTemporaryAugmentIdsCsv);
        _state.PreviewPermanentAugmentIdsCsv = EditorGUILayout.TextField("Perm Augments CSV", _state.PreviewPermanentAugmentIdsCsv);
        _state.PreviewScoutDirectiveKind = (ScoutDirectiveKind)EditorGUILayout.EnumPopup("Preview Scout", _state.PreviewScoutDirectiveKind);
        if (_state.PreviewScoutDirectiveKind == ScoutDirectiveKind.SynergyTag)
        {
            _state.PreviewScoutSynergyTagId = EditorGUILayout.TextField("Preview Scout Tag", _state.PreviewScoutSynergyTagId);
        }

        _state.PreviewRarePity = EditorGUILayout.IntField("Rare Pity Packs", _state.PreviewRarePity);
        _state.PreviewEpicPity = EditorGUILayout.IntField("Epic Pity Packs", _state.PreviewEpicPity);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reset Preview State"))
            {
                ResetPreviewState();
            }

            if (GUILayout.Button("Generate Pack"))
            {
                GeneratePreviewPack(consumeRefresh: false);
            }

            if (GUILayout.Button("Refresh Preview Pack"))
            {
                GeneratePreviewPack(consumeRefresh: true);
            }
        }

        _state.LastPackReport = EditorGUILayout.TextArea(_state.LastPackReport, GUILayout.MinHeight(220f));
    }

    private void DrawRetrainSection()
    {
        var hero = DrawSelectedHeroPopup("Retrain Hero");
        if (hero == null)
        {
            EditorGUILayout.HelpBox("선택 가능한 hero가 없습니다.", MessageType.Warning);
            return;
        }

        _state.RetrainOperation = (RetrainOperationKind)EditorGUILayout.EnumPopup("Operation", _state.RetrainOperation);

        var template = _lookup.Snapshot.Archetypes[hero.ArchetypeId];
        var currentActiveId = string.IsNullOrWhiteSpace(hero.FlexActiveId)
            ? template.FlexActive?.Id ?? template.RecruitFlexActivePool?.FirstOrDefault()?.Id ?? string.Empty
            : hero.FlexActiveId;
        var currentPassiveId = string.IsNullOrWhiteSpace(hero.FlexPassiveId)
            ? template.FlexPassive?.Id ?? template.RecruitFlexPassivePool?.FirstOrDefault()?.Id ?? string.Empty
            : hero.FlexPassiveId;
        var retrainState = hero.RetrainState?.Clone() ?? new UnitRetrainState();
        var nextCost = RecruitmentBalanceCatalog.DefaultRetrainCosts.GetTotalCost(_state.RetrainOperation, retrainState);

        EditorGUILayout.HelpBox(
            $"Current  flexA={currentActiveId}  flexP={currentPassiveId}\n" +
            $"Memory  prevA={retrainState.PreviousFlexActiveId}  prevP={retrainState.PreviousFlexPassiveId}\n" +
            $"State  count={retrainState.RetrainCount}  planMiss={retrainState.ConsecutivePlanIncoherentRetrains}  totalEcho={retrainState.TotalEchoSpent}\n" +
            $"Next Cost  {nextCost} Echo",
            MessageType.None);

        if (GUILayout.Button("Run Retrain"))
        {
            var result = _session.RetrainHero(hero.HeroId, _state.RetrainOperation);
            _state.LastRetrainReport = result.IsSuccess
                ? BuildRetrainReport(hero.HeroId)
                : $"Retrain 실패\n{result.Error}";
        }

        _state.LastRetrainReport = EditorGUILayout.TextArea(_state.LastRetrainReport, GUILayout.MinHeight(120f));
    }

    private void DrawDuplicateSection()
    {
        var ownedArchetypes = _session.Profile.Heroes
            .Select(hero => hero.ArchetypeId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (ownedArchetypes.Length > 0)
        {
            var currentIndex = Array.IndexOf(ownedArchetypes, string.IsNullOrWhiteSpace(_state.DuplicateGrantArchetypeId) ? ownedArchetypes[0] : _state.DuplicateGrantArchetypeId);
            currentIndex = currentIndex < 0 ? 0 : currentIndex;
            currentIndex = EditorGUILayout.Popup("Owned Blueprint", currentIndex, ownedArchetypes);
            _state.DuplicateGrantArchetypeId = ownedArchetypes[currentIndex];
        }
        else
        {
            _state.DuplicateGrantArchetypeId = EditorGUILayout.TextField("Owned Blueprint", _state.DuplicateGrantArchetypeId);
        }

        if (GUILayout.Button("Grant Duplicate"))
        {
            var beforeHeroCount = _session.Profile.Heroes.Count;
            var beforeEcho = _session.Profile.Currencies.Echo;
            var result = _session.GrantHeroDirect(_state.DuplicateGrantArchetypeId, RecruitOfferSource.DirectGrant);
            var conversion = _session.LastDuplicateConversion;
            _state.LastDuplicateReport = result.IsSuccess
                ? $"Duplicate 처리 성공\nunit={_state.DuplicateGrantArchetypeId}\nheroCount {beforeHeroCount} -> {_session.Profile.Heroes.Count}\necho {beforeEcho} -> {_session.Profile.Currencies.Echo}\nresolution={conversion?.Resolution}\ntier={conversion?.SourceTier}\nechoGranted={conversion?.EchoGranted}"
                : $"Duplicate 처리 실패\n{result.Error}";
        }

        _state.LastDuplicateReport = EditorGUILayout.TextArea(_state.LastDuplicateReport, GUILayout.MinHeight(100f));
    }

    private void DrawDismissSection()
    {
        var hero = DrawSelectedHeroPopup("Dismiss Hero");
        if (hero == null)
        {
            EditorGUILayout.HelpBox("선택 가능한 hero가 없습니다.", MessageType.Warning);
            return;
        }

        var footprint = hero.EconomyFootprint ?? new UnitEconomyFootprint();
        EditorGUILayout.HelpBox(
            $"Footprint  recruitGold={footprint.RecruitGoldPaid}  retrainEcho={footprint.RetrainEchoPaid}\n" +
            $"Equipped  {string.Join(", ", hero.EquippedItemIds)}",
            MessageType.None);

        if (GUILayout.Button("Dismiss Selected Hero"))
        {
            var beforeGold = _session.Profile.Currencies.Gold;
            var beforeEcho = _session.Profile.Currencies.Echo;
            var beforeInventory = _session.Profile.Inventory.Count(item => hero.EquippedItemIds.Contains(item.ItemInstanceId) && !string.IsNullOrWhiteSpace(item.EquippedHeroId));
            var result = _session.DismissHero(hero.HeroId);
            var afterInventory = _session.Profile.Inventory.Count(item => hero.EquippedItemIds.Contains(item.ItemInstanceId) && !string.IsNullOrWhiteSpace(item.EquippedHeroId));

            _state.LastDismissReport = result.IsSuccess
                ? $"Dismiss 성공\nhero={hero.HeroId}\nGold refund={_session.Profile.Currencies.Gold - beforeGold}\nEcho refund={_session.Profile.Currencies.Echo - beforeEcho}\nEquipped items released={beforeInventory - afterInventory}"
                : $"Dismiss 실패\n{result.Error}";
        }

        _state.LastDismissReport = EditorGUILayout.TextArea(_state.LastDismissReport, GUILayout.MinHeight(100f));
    }

    private void DrawSection(string title, Action drawBody)
    {
        using var scope = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(2f);
        drawBody();
    }

    private HeroInstanceRecord? DrawSelectedHeroPopup(string label)
    {
        var heroes = _session.Profile.Heroes;
        if (heroes.Count == 0)
        {
            return null;
        }

        _state.SelectedHeroIndex = Mathf.Clamp(_state.SelectedHeroIndex, 0, heroes.Count - 1);
        var options = heroes.Select(hero => $"{hero.Name} [{hero.ArchetypeId}]").ToArray();
        _state.SelectedHeroIndex = EditorGUILayout.Popup(label, _state.SelectedHeroIndex, options);
        return heroes[_state.SelectedHeroIndex];
    }

    private void ResetRuntimeSession()
    {
        _lookup = new RuntimeCombatContentLookup();
        _session = new GameSessionState(_lookup);
        _session.BindProfile(new SaveProfile());
        _session.SetCurrentScene(SceneNames.Town);
        _session.Profile.Currencies.Gold = Math.Max(_session.Profile.Currencies.Gold, 200);
        _session.Profile.Currencies.Echo = Math.Max(_session.Profile.Currencies.Echo, 200);

        if (string.IsNullOrWhiteSpace(_state?.PreviewRosterArchetypeIdsCsv))
        {
            _state.PreviewRosterArchetypeIdsCsv = string.Join(",", _session.Profile.Heroes.Take(4).Select(hero => hero.ArchetypeId));
        }

        if (string.IsNullOrWhiteSpace(_state?.DuplicateGrantArchetypeId))
        {
            _state.DuplicateGrantArchetypeId = _session.Profile.Heroes.FirstOrDefault()?.ArchetypeId ?? string.Empty;
        }
    }

    private void ResetPreviewState()
    {
        _previewGeneration = 0;
        _previewPhaseState = new RecruitPhaseState();
        _previewPityState = new RecruitPityState
        {
            PacksSinceRarePlusSeen = Mathf.Max(0, _state.PreviewRarePity),
            PacksSinceEpicSeen = Mathf.Max(0, _state.PreviewEpicPity),
        };
        _state.LastPackReport = string.Empty;
    }

    private void GeneratePreviewPack(bool consumeRefresh)
    {
        try
        {
            if (consumeRefresh)
            {
                _previewPhaseState = RefreshCostService.ConsumeRefresh(_previewPhaseState);
                _previewGeneration++;
            }

            var roster = BuildPreviewRoster();
            var tempAugments = ParseCsv(_state.PreviewTemporaryAugmentIdsCsv);
            var permAugments = ParseCsv(_state.PreviewPermanentAugmentIdsCsv);
            var result = RecruitPackGenerator.GeneratePack(
                _lookup.Snapshot.Archetypes,
                _lookup.Snapshot,
                roster,
                tempAugments,
                permAugments,
                _previewPityState,
                ClonePreviewPhaseWithDirective(),
                _state.PreviewSeed + _previewGeneration);

            _previewPityState = result.UpdatedPity;
            _previewPhaseState = result.UpdatedPhase;
            _state.LastPackReport = BuildPackReport(roster, tempAugments, permAugments, result);
        }
        catch (Exception ex)
        {
            _state.LastPackReport = ex.Message;
        }
    }

    private string BuildPackReport(
        IReadOnlyList<HeroRecord> roster,
        IReadOnlyList<string> temporaryAugments,
        IReadOnlyList<string> permanentAugments,
        RecruitPackGenerationResult result)
    {
        var directive = CloneDirective(_state.PreviewScoutDirectiveKind, _state.PreviewScoutSynergyTagId);
        var plan = TeamPlanEvaluator.Evaluate(roster, _lookup.Snapshot.Archetypes, _lookup.Snapshot, temporaryAugments, permanentAugments);
        var builder = new StringBuilder();
        builder.AppendLine($"seed={_state.PreviewSeed + _previewGeneration}");
        builder.AppendLine($"roster={string.Join(", ", roster.Select(hero => hero.ArchetypeId))}");
        builder.AppendLine($"plan={FormatPlan(plan)}");
        builder.AppendLine($"directive={FormatDirective(directive)}");
        builder.AppendLine($"phase free={result.UpdatedPhase.FreeRefreshesRemaining} paid={result.UpdatedPhase.PaidRefreshCountThisPhase} scoutUsed={result.UpdatedPhase.ScoutUsedThisPhase}");
        builder.AppendLine($"pity rare={result.UpdatedPity.PacksSinceRarePlusSeen}/3 epic={result.UpdatedPity.PacksSinceEpicSeen}/8");
        builder.AppendLine();

        foreach (var offer in result.Offers)
        {
            builder.AppendLine($"[{offer.Metadata.SlotType}] {offer.UnitBlueprintId}");
            builder.AppendLine($"  tier={offer.Metadata.Tier} fit={offer.Metadata.PlanFit} cost={offer.Metadata.GoldCost} pity={offer.Metadata.ProtectedByPity} scout={offer.Metadata.BiasedByScout}");
            builder.AppendLine($"  flexA={offer.FlexActiveId} flexP={offer.FlexPassiveId}");
            builder.AppendLine(
                $"  score total={offer.Metadata.PlanScore.Total} breakpoint={offer.Metadata.PlanScore.BreakpointProgressScore} native={offer.Metadata.PlanScore.NativeTagMatchScore} role={offer.Metadata.PlanScore.RoleNeedScore} augment={offer.Metadata.PlanScore.AugmentHookScore} scout={offer.Metadata.PlanScore.ScoutDirectiveScore} over={offer.Metadata.PlanScore.OversaturationPenalty}");
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildRetrainReport(string heroId)
    {
        var hero = _session.Profile.Heroes.First(record => record.HeroId == heroId);
        var template = _lookup.Snapshot.Archetypes[hero.ArchetypeId];
        var plan = _session.CurrentTeamPlan;
        var active = template.RecruitFlexActivePool?.FirstOrDefault(skill => string.Equals(skill.Id, hero.FlexActiveId, StringComparison.Ordinal));
        var passive = template.RecruitFlexPassivePool?.FirstOrDefault(skill => string.Equals(skill.Id, hero.FlexPassiveId, StringComparison.Ordinal));
        var builder = new StringBuilder();
        builder.AppendLine($"hero={hero.Name} [{hero.ArchetypeId}]");
        builder.AppendLine($"wallet Gold={_session.Profile.Currencies.Gold} Echo={_session.Profile.Currencies.Echo}");
        builder.AppendLine($"flexA={hero.FlexActiveId} native={IsCoherent(template, active, plan)}");
        builder.AppendLine($"flexP={hero.FlexPassiveId} native={IsCoherent(template, passive, plan)}");
        builder.AppendLine($"prevA={hero.RetrainState?.PreviousFlexActiveId} prevP={hero.RetrainState?.PreviousFlexPassiveId}");
        builder.AppendLine($"retrainCount={hero.RetrainState?.RetrainCount ?? 0} planMiss={hero.RetrainState?.ConsecutivePlanIncoherentRetrains ?? 0}");
        builder.AppendLine($"footprint recruitGold={hero.EconomyFootprint?.RecruitGoldPaid ?? 0} retrainEcho={hero.EconomyFootprint?.RetrainEchoPaid ?? 0}");
        return builder.ToString().TrimEnd();
    }

    private IReadOnlyList<HeroRecord> BuildPreviewRoster()
    {
        var archetypeIds = ParseCsv(_state.PreviewRosterArchetypeIdsCsv);
        if (archetypeIds.Count == 0)
        {
            return _session.Profile.Heroes.Take(4).Select(ToHeroRecord).ToList();
        }

        var roster = new List<HeroRecord>();
        for (var index = 0; index < archetypeIds.Count; index++)
        {
            if (!_lookup.Snapshot.Archetypes.TryGetValue(archetypeIds[index], out var template))
            {
                continue;
            }

            var flexActiveId = template.FlexActive?.Id ?? template.RecruitFlexActivePool?.FirstOrDefault()?.Id ?? string.Empty;
            var flexPassiveId = template.FlexPassive?.Id ?? template.RecruitFlexPassivePool?.FirstOrDefault()?.Id ?? string.Empty;
            roster.Add(new HeroRecord(
                $"preview-{index + 1}",
                template.DisplayName,
                template.Id,
                template.RaceId,
                template.ClassId,
                string.Empty,
                string.Empty,
                flexActiveId,
                flexPassiveId,
                template.RecruitTier,
                RecruitOfferSource.RecruitPhase,
                new UnitRetrainState(),
                new UnitEconomyFootprint()));
        }

        return roster;
    }

    private RecruitPhaseState ClonePreviewPhaseWithDirective()
    {
        var clone = _previewPhaseState.Clone();
        clone.PendingScoutDirective = CloneDirective(_state.PreviewScoutDirectiveKind, _state.PreviewScoutSynergyTagId);
        return clone;
    }

    private static ScoutDirective CloneDirective(ScoutDirectiveKind kind, string synergyTagId)
    {
        return new ScoutDirective
        {
            Kind = kind,
            SynergyTagId = synergyTagId ?? string.Empty,
        };
    }

    private static IReadOnlyList<string> ParseCsv(string csv)
    {
        return string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',')
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
    }

    private static string FormatPlan(TeamPlanProfile plan)
    {
        return $"tags=[{string.Join(", ", plan.TopSynergyTagIds)}] needs(front={plan.NeedsFrontline}, back={plan.NeedsBackline}, support={plan.NeedsSupport}) dmg(phys={plan.PrefersPhysical}, mag={plan.PrefersMagical}) aug=[{string.Join(", ", plan.AugmentHookTags)}]";
    }

    private static string FormatDirective(ScoutDirective directive)
    {
        if (directive == null || directive.IsNone)
        {
            return "None";
        }

        return directive.Kind == ScoutDirectiveKind.SynergyTag
            ? $"{directive.Kind}:{directive.SynergyTagId}"
            : directive.Kind.ToString();
    }

    private static HeroRecord ToHeroRecord(HeroInstanceRecord hero)
    {
        return new HeroRecord(
            hero.HeroId,
            hero.Name,
            hero.ArchetypeId,
            hero.RaceId,
            hero.ClassId,
            hero.PositiveTraitId,
            hero.NegativeTraitId,
            hero.FlexActiveId,
            hero.FlexPassiveId,
            hero.RecruitTier,
            hero.RecruitSource,
            hero.RetrainState?.Clone() ?? new UnitRetrainState(),
            hero.EconomyFootprint?.Clone() ?? new UnitEconomyFootprint());
    }

    private static string IsCoherent(CombatArchetypeTemplate template, BattleSkillSpec? option, TeamPlanProfile plan)
    {
        if (option == null)
        {
            return "missing";
        }

        var native = RecruitmentTemplateResolver.IsNativeCoherent(template, option);
        var coherent = RecruitmentTemplateResolver.IsPlanCoherent(template, option, plan);
        return $"native={native} plan={coherent}";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;
using SM.Unity.UI;

namespace SM.Unity.UI.Atlas;

/// <summary>
/// 출격 편성 확인 Presenter — 6 anchor 배치와 활성 시너지를 view-state로 변환한다.
/// synergy 집계는 LoadoutCompiler/도감과 동일한 LaunchCoreRosterBaselineCatalog.ResolveSynergyThresholds
/// (race 2/4 · class 2/3 baseline lock)를 read-only로 재사용한다 — 새 synergy 규칙을 만들지 않는다.
///
/// 테스트성: 이름 해석(archetype/synergy/anchor)은 concrete resolver 대신 Func 델리게이트로 주입받아
/// FastUnit이 무거운 localization 없이 검증 가능하게 한다(WarrantSelectionPresenter 패턴 재사용).
/// </summary>
public sealed class SortieConfirmPresenter
{
    private readonly GameSessionState _session;
    private readonly LaunchCoreRosterBaselineCatalog _baselineCatalog;
    private readonly Func<string, string> _archetypeName;
    private readonly Func<string, string> _synergyName;
    private readonly Func<DeploymentAnchorId, string> _anchorName;
    private readonly Func<string, string, string>? _characterName;
    // 흉상 resolve — FastUnit 경량화를 위해 optional(default null). 런타임은 ResolveCharacterPortrait 주입.
    private readonly Func<string, UnityEngine.Texture2D?>? _portraitSprite;

    public SortieConfirmPresenter(
        GameSessionState session,
        LaunchCoreRosterBaselineCatalog baselineCatalog,
        Func<string, string> archetypeName,
        Func<string, string> synergyName,
        Func<DeploymentAnchorId, string> anchorName,
        Func<string, UnityEngine.Texture2D?>? portraitSprite = null,
        Func<string, string, string>? characterName = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _baselineCatalog = baselineCatalog ?? throw new ArgumentNullException(nameof(baselineCatalog));
        _archetypeName = archetypeName ?? throw new ArgumentNullException(nameof(archetypeName));
        _synergyName = synergyName ?? throw new ArgumentNullException(nameof(synergyName));
        _anchorName = anchorName ?? throw new ArgumentNullException(nameof(anchorName));
        _portraitSprite = portraitSprite;
        _characterName = characterName;
    }

    public SortieConfirmViewState BuildState()
    {
        var heroesById = _session.Profile.Heroes
            .Where(hero => !string.IsNullOrWhiteSpace(hero.HeroId))
            .GroupBy(hero => hero.HeroId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var slots = new List<SortieDeploySlotViewState>();
        foreach (var anchor in _session.DeploymentAnchors)
        {
            var heroId = _session.GetAssignedHeroId(anchor);
            if (!string.IsNullOrWhiteSpace(heroId) && heroesById.TryGetValue(heroId!, out var hero))
            {
                var portraitKey = string.IsNullOrWhiteSpace(hero.CharacterId) ? hero.ArchetypeId : hero.CharacterId;
                slots.Add(new SortieDeploySlotViewState(
                    anchor,
                    _anchorName(anchor),
                    HeroDisplayLabelFormatter.ResolvePersonName(hero, _characterName),
                    _archetypeName(hero.ArchetypeId),
                    IsEmpty: false,
                    PortraitSprite: string.IsNullOrWhiteSpace(portraitKey) ? null : _portraitSprite?.Invoke(portraitKey)));
            }
            else
            {
                slots.Add(new SortieDeploySlotViewState(
                    anchor,
                    _anchorName(anchor),
                    "(비어 있음)",
                    string.Empty,
                    IsEmpty: true));
            }
        }

        var deployRecords = _session.BattleDeployHeroIds
            .Where(heroesById.ContainsKey)
            .Select(id => heroesById[id])
            .ToList();

        var synergyChips = BuildSynergyChips(deployRecords);
        var canLaunch = deployRecords.Count > 0;

        return new SortieConfirmViewState(
            Title: "출격 편성 확인",
            DeployHeader: $"출전 배치 {deployRecords.Count}/{MetaBalanceDefaults.BattleDeployCap}",
            DeployHint: "슬롯을 누를 때마다 다음 동료로 순환합니다 (계속 누르면 '비어 있음'까지).",
            DeploySlots: slots,
            DeployCount: deployRecords.Count,
            DeployCap: MetaBalanceDefaults.BattleDeployCap,
            SynergyChips: synergyChips,
            SynergyEmptyText: "활성 시너지 없음 — 같은 종족/병종을 모으면 시너지가 켜집니다.",
            CanLaunch: canLaunch,
            LaunchLabel: canLaunch ? "이대로 출격" : "최소 1명 배치 필요",
            EditLabel: "마을에서 상세 편성",
            CancelLabel: "돌아가기");
    }

    private IReadOnlyList<SortieSynergyChipViewState> BuildSynergyChips(IReadOnlyList<HeroInstanceRecord> deploy)
    {
        var chips = new List<SortieSynergyChipViewState>();
        AppendFamilyChips(chips, deploy, hero => hero.RaceId, isClassFamily: false);
        AppendFamilyChips(chips, deploy, hero => hero.ClassId, isClassFamily: true);
        return chips;
    }

    private void AppendFamilyChips(
        List<SortieSynergyChipViewState> chips,
        IReadOnlyList<HeroInstanceRecord> deploy,
        Func<HeroInstanceRecord, string> familySelector,
        bool isClassFamily)
    {
        foreach (var group in deploy
                     .GroupBy(familySelector, StringComparer.Ordinal)
                     .Where(group => !string.IsNullOrWhiteSpace(group.Key)))
        {
            var synergyId = LaunchCoreRosterBaselineCatalog.BuildSynergyId(group.Key);
            var (minor, major) = _baselineCatalog.ResolveSynergyThresholds(synergyId, isClassFamily);
            var count = group.Count();
            // breakpoint 미달(count<minor)도 회색 칩으로 노출 — "한 명만 더 모으면 켜진다"를 가르친다(수집 동기).
            // 활성 여부는 IsActive로 내려 View가 회색/강조를 구분한다.
            chips.Add(new SortieSynergyChipViewState(
                _synergyName(synergyId),
                count,
                minor,
                major,
                MajorReached: count >= major,
                IsActive: count >= minor));
        }
    }
}

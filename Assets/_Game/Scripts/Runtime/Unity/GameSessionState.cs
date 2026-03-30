using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Results;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

public sealed class GameSessionState
{
    private static readonly DeploymentAnchorId[] DeploymentAnchorOrder =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
        DeploymentAnchorId.BackCenter,
        DeploymentAnchorId.BackBottom
    };

    private readonly RuntimeCombatContentLookup _combatContentLookup;
    private readonly List<string> _expeditionSquadHeroIds = new();
    private readonly Dictionary<DeploymentAnchorId, string?> _deploymentAssignments = new();
    private readonly List<RecruitOffer> _recruitOffers = new();
    private readonly List<ExpeditionNodeViewModel> _expeditionNodes = new();
    private readonly List<RewardChoiceViewModel> _pendingRewardChoices = new();
    private readonly HashSet<string> _resolvedExpeditionNodeIds = new(StringComparer.Ordinal);
    private int _recruitOfferGeneration;

    public SaveProfile Profile { get; private set; } = new();
    public RosterState Roster { get; private set; } = new();
    public ExpeditionState Expedition { get; private set; } = new();
    public string CurrentSceneName { get; private set; } = SceneNames.Boot;
    public int PermanentAugmentSlotCount { get; private set; } = 1;
    public int CurrentExpeditionNodeIndex { get; private set; }
    public int? SelectedExpeditionNodeIndex { get; private set; }
    public bool LastBattleVictory { get; private set; }
    public bool IsQuickBattleSmokeActive { get; private set; }
    public bool HasActiveExpeditionRun { get; private set; }
    public string LastBattleSummary { get; private set; } = string.Empty;
    public string LastExpeditionEffectMessage { get; private set; } = string.Empty;
    public string LastRewardApplicationSummary { get; private set; } = string.Empty;
    public TeamPostureType SelectedTeamPosture { get; private set; } = TeamPostureType.StandardAdvance;
    public IReadOnlyList<string> ExpeditionSquadHeroIds => _expeditionSquadHeroIds;
    public IReadOnlyList<DeploymentAnchorId> DeploymentAnchors => DeploymentAnchorOrder;
    public IReadOnlyList<string> BattleDeployHeroIds => DeploymentAnchorOrder
        .Select(anchor => _deploymentAssignments.TryGetValue(anchor, out var heroId) ? heroId : null)
        .Where(heroId => !string.IsNullOrWhiteSpace(heroId))
        .Cast<string>()
        .ToList();
    public IReadOnlyDictionary<DeploymentAnchorId, string?> DeploymentAssignments => _deploymentAssignments;
    public IReadOnlyList<RecruitOffer> RecruitOffers => _recruitOffers;
    public IReadOnlyList<ExpeditionNodeViewModel> ExpeditionNodes => _expeditionNodes;
    public IReadOnlyList<RewardChoiceViewModel> PendingRewardChoices => _pendingRewardChoices;
    public bool CanResumeExpedition => HasActiveExpeditionRun && !IsQuickBattleSmokeActive && CurrentExpeditionNodeIndex < _expeditionNodes.Count - 1;

    public GameSessionState(RuntimeCombatContentLookup combatContentLookup)
    {
        _combatContentLookup = combatContentLookup;
    }

    public void BindProfile(SaveProfile profile)
    {
        Profile = profile;
        Profile.Heroes ??= new List<HeroInstanceRecord>();

        if (Profile.Heroes.Count == 0)
        {
            SeedDemoProfile();
        }

        Profile.Currencies ??= new CurrencyRecord();
        Profile.Inventory ??= new List<InventoryItemRecord>();
        Profile.UnlockedPermanentAugmentIds ??= new List<string>();
        Profile.RunSummaries ??= new List<RunSummaryRecord>();
        NormalizeProfileContentIds();

        if (string.IsNullOrWhiteSpace(Profile.DisplayName))
        {
            Profile.DisplayName = "Player";
        }

        PermanentAugmentSlotCount = Math.Max(1, Profile.UnlockedPermanentAugmentIds.Count);
        CurrentExpeditionNodeIndex = 0;
        SelectedExpeditionNodeIndex = null;
        LastBattleVictory = false;
        IsQuickBattleSmokeActive = false;
        HasActiveExpeditionRun = false;
        LastBattleSummary = string.Empty;
        LastExpeditionEffectMessage = string.Empty;
        LastRewardApplicationSummary = string.Empty;
        SelectedTeamPosture = TeamPostureType.StandardAdvance;
        _recruitOfferGeneration = 0;
        _resolvedExpeditionNodeIds.Clear();
        ResetDeploymentAssignments();

        Roster = new RosterState(ToHeroRecords(Profile));
        EnsureRecruitOffers();
        EnsureDefaultSquad();
        EnsureBattleDeployReady();
        EnsureExpeditionNodes(reset: true);
        MarkCurrentNodeResolved();
        AutoSelectNextExpeditionNode();
        EnsureRewardChoices(reset: true);
        SyncExpeditionState();
    }

    public void BeginNewExpedition()
    {
        IsQuickBattleSmokeActive = false;
        HasActiveExpeditionRun = true;
        CurrentExpeditionNodeIndex = 0;
        SelectedExpeditionNodeIndex = null;
        LastBattleVictory = false;
        LastBattleSummary = string.Empty;
        LastExpeditionEffectMessage = string.Empty;
        LastRewardApplicationSummary = string.Empty;
        _resolvedExpeditionNodeIds.Clear();
        EnsureBattleDeployReady();
        EnsureExpeditionNodes(reset: true);
        MarkCurrentNodeResolved();
        AutoSelectNextExpeditionNode();
        EnsureRewardChoices(reset: true);
        Expedition = new ExpeditionState(CurrentExpeditionNodeIndex);
    }

    public void PrepareQuickBattleSmoke()
    {
        IsQuickBattleSmokeActive = true;
        HasActiveExpeditionRun = false;
        LastBattleVictory = false;
        LastBattleSummary = "Quick Battle smoke";
        LastExpeditionEffectMessage = string.Empty;
        LastRewardApplicationSummary = string.Empty;
        EnsureDefaultSquad();
        EnsureBattleDeployReady();
        EnsureRewardChoices(reset: true);
    }

    public void AdvanceExpeditionNode()
    {
        ResolveSelectedExpeditionNode();
    }

    public bool SelectNextExpeditionNode(int nodeIndex)
    {
        EnsureExpeditionNodes();
        var current = GetCurrentExpeditionNode();
        if (current == null || !current.NextNodeIndices.Contains(nodeIndex))
        {
            return false;
        }

        SelectedExpeditionNodeIndex = nodeIndex;
        return true;
    }

    public ExpeditionNodeViewModel? GetCurrentExpeditionNode()
    {
        EnsureExpeditionNodes();
        return CurrentExpeditionNodeIndex >= 0 && CurrentExpeditionNodeIndex < _expeditionNodes.Count
            ? _expeditionNodes[CurrentExpeditionNodeIndex]
            : null;
    }

    public ExpeditionNodeViewModel? GetSelectedExpeditionNode()
    {
        EnsureExpeditionNodes();
        return SelectedExpeditionNodeIndex is int index && index >= 0 && index < _expeditionNodes.Count
            ? _expeditionNodes[index]
            : null;
    }

    public IReadOnlyList<int> GetSelectableNextNodeIndices()
    {
        var current = GetCurrentExpeditionNode();
        return current?.NextNodeIndices ?? Array.Empty<int>();
    }

    public bool ResolveSelectedExpeditionNode()
    {
        var selected = GetSelectedExpeditionNode();
        if (selected == null)
        {
            return false;
        }

        CurrentExpeditionNodeIndex = selected.Index;
        MarkCurrentNodeResolved();
        LastExpeditionEffectMessage = ApplyExpeditionNodeEffect(selected);
        SyncExpeditionState();
        AutoSelectNextExpeditionNode();
        return true;
    }

    public void AbandonExpeditionRun()
    {
        IsQuickBattleSmokeActive = false;
        HasActiveExpeditionRun = false;
        SelectedExpeditionNodeIndex = null;
        LastExpeditionEffectMessage = "원정을 종료하고 Town으로 복귀합니다.";
    }

    public void ReturnToTownAfterReward()
    {
        IsQuickBattleSmokeActive = false;
        if (!HasActiveExpeditionRun || CurrentExpeditionNodeIndex >= _expeditionNodes.Count - 1)
        {
            HasActiveExpeditionRun = false;
        }
    }

    public void SetCurrentScene(string sceneName)
    {
        CurrentSceneName = sceneName;
    }

    public Result RerollRecruitOffers()
    {
        if (Profile.Currencies.Gold < MetaBalanceDefaults.RecruitRerollCost)
        {
            return Result.Fail($"Gold가 부족합니다. 리롤에는 {MetaBalanceDefaults.RecruitRerollCost} Gold가 필요합니다.");
        }

        Profile.Currencies.Gold -= MetaBalanceDefaults.RecruitRerollCost;
        _recruitOfferGeneration += 1;
        _recruitOffers.Clear();
        EnsureRecruitOffers();
        return Result.Success();
    }

    public Result Recruit(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= _recruitOffers.Count)
        {
            return Result.Fail("유효하지 않은 영입 후보입니다.");
        }

        if (Profile.Currencies.Gold < MetaBalanceDefaults.RecruitCost)
        {
            return Result.Fail($"Gold가 부족합니다. 영입에는 {MetaBalanceDefaults.RecruitCost} Gold가 필요합니다.");
        }

        if (Profile.Heroes.Count >= MetaBalanceDefaults.TownRosterCap)
        {
            return Result.Fail($"Town roster cap {MetaBalanceDefaults.TownRosterCap}에 도달했습니다.");
        }

        var offer = _recruitOffers[offerIndex];
        Profile.Currencies.Gold -= MetaBalanceDefaults.RecruitCost;
        Profile.Heroes.Add(new HeroInstanceRecord
        {
            HeroId = offer.HeroId,
            Name = offer.Name,
            ArchetypeId = offer.ArchetypeId,
            RaceId = offer.RaceId,
            ClassId = offer.ClassId,
            PositiveTraitId = offer.PositiveTraitId,
            NegativeTraitId = offer.NegativeTraitId,
            EquippedItemIds = new List<string>()
        });

        Roster = new RosterState(ToHeroRecords(Profile));
        _recruitOffers.RemoveAt(offerIndex);
        EnsureRecruitOffers();
        return Result.Success();
    }

    public bool ToggleExpeditionHero(string heroId)
    {
        if (_expeditionSquadHeroIds.Contains(heroId))
        {
            _expeditionSquadHeroIds.Remove(heroId);
            ClearDeploymentForHero(heroId);
            EnsureBattleDeployReady();
            return true;
        }

        if (_expeditionSquadHeroIds.Count >= MetaBalanceDefaults.ExpeditionSquadCap)
        {
            return false;
        }

        _expeditionSquadHeroIds.Add(heroId);
        EnsureBattleDeployReady();
        return true;
    }

    public void EnsureBattleDeployReady()
    {
        EnsureDefaultSquad();
        EnsureDefaultDeploymentAssignments();
    }

    public void PromoteToBattleDeploy(string heroId)
    {
        if (!_expeditionSquadHeroIds.Contains(heroId))
        {
            return;
        }

        var preferredAnchor = ResolvePreferredAnchor(heroId);
        AssignHeroToAnchor(preferredAnchor, heroId);
    }

    public string? GetAssignedHeroId(DeploymentAnchorId anchor)
    {
        return _deploymentAssignments.TryGetValue(anchor, out var heroId) ? heroId : null;
    }

    public bool AssignHeroToAnchor(DeploymentAnchorId anchor, string? heroId)
    {
        EnsureDefaultSquad();
        EnsureAssignmentMapInitialized();

        heroId = string.IsNullOrWhiteSpace(heroId) ? null : heroId;
        if (heroId != null && !_expeditionSquadHeroIds.Contains(heroId))
        {
            return false;
        }

        var currentHero = GetAssignedHeroId(anchor);
        var occupiedBefore = BattleDeployHeroIds.Count;
        var candidateAlreadyAssigned = heroId != null && BattleDeployHeroIds.Contains(heroId);
        var occupiedAfter = occupiedBefore
            - (string.IsNullOrWhiteSpace(currentHero) ? 0 : 1)
            + (heroId == null || candidateAlreadyAssigned ? 0 : 1);
        if (occupiedAfter > MetaBalanceDefaults.BattleDeployCap)
        {
            return false;
        }

        if (heroId != null)
        {
            foreach (var existingAnchor in DeploymentAnchorOrder)
            {
                if (existingAnchor == anchor)
                {
                    continue;
                }

                if (_deploymentAssignments.TryGetValue(existingAnchor, out var existingHero) && existingHero == heroId)
                {
                    _deploymentAssignments[existingAnchor] = null;
                }
            }
        }

        _deploymentAssignments[anchor] = heroId;
        return true;
    }

    public bool CycleDeploymentAssignment(DeploymentAnchorId anchor)
    {
        EnsureBattleDeployReady();

        var candidates = new List<string?> { null };
        candidates.AddRange(_expeditionSquadHeroIds);

        var current = GetAssignedHeroId(anchor);
        var startIndex = candidates.FindIndex(candidate => candidate == current);
        startIndex = startIndex < 0 ? 0 : startIndex;

        for (var offset = 1; offset <= candidates.Count; offset++)
        {
            var candidate = candidates[(startIndex + offset) % candidates.Count];
            if (AssignHeroToAnchor(anchor, candidate))
            {
                return true;
            }
        }

        return false;
    }

    public void CycleTeamPosture()
    {
        var values = (TeamPostureType[])Enum.GetValues(typeof(TeamPostureType));
        var currentIndex = Array.IndexOf(values, SelectedTeamPosture);
        SelectedTeamPosture = values[(currentIndex + 1) % values.Length];
    }

    public IEnumerable<(DeploymentAnchorId Anchor, string? HeroId)> EnumerateDeploymentAssignments()
    {
        EnsureBattleDeployReady();
        foreach (var anchor in DeploymentAnchorOrder)
        {
            yield return (anchor, GetAssignedHeroId(anchor));
        }
    }

    public IReadOnlyList<BattleParticipantSpec> BuildBattleParticipants()
    {
        EnsureBattleDeployReady();

        var temporaryAugments = Expedition.TemporaryAugmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var inventoryByInstanceId = Profile.Inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemInstanceId))
            .GroupBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return EnumerateDeploymentAssignments()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.HeroId))
            .Select(entry =>
            {
                var hero = Profile.Heroes.First(record => record.HeroId == entry.HeroId);
                return new BattleParticipantSpec(
                    hero.HeroId,
                    hero.Name,
                    hero.ArchetypeId,
                    entry.Anchor,
                    hero.PositiveTraitId,
                    hero.NegativeTraitId,
                    BuildEquippedItemSpecs(hero, inventoryByInstanceId),
                    temporaryAugments);
            })
            .ToList();
    }

    public void SaveDebugSnapshot(string note = "manual-debug-save")
    {
        Profile.RunSummaries.Add(new RunSummaryRecord
        {
            RunId = Guid.NewGuid().ToString("N"),
            ExpeditionId = note,
            Result = "debug-save",
            GoldEarned = 0,
            NodesCleared = CurrentExpeditionNodeIndex,
            CompletedAtUtc = DateTime.UtcNow.ToString("O")
        });
    }

    public void SetLastBattleResult(bool victory, string summary)
    {
        LastBattleVictory = victory;
        LastBattleSummary = summary;
        LastRewardApplicationSummary = string.Empty;
        EnsureRewardChoices(reset: true);
    }

    public void MarkBattleResolved(bool victory, string summary)
    {
        LastBattleVictory = victory;
        LastRewardApplicationSummary = string.Empty;

        if (victory && !IsQuickBattleSmokeActive)
        {
            var moved = ResolveSelectedExpeditionNode();
            var currentNode = GetCurrentExpeditionNode();
            if (moved && currentNode != null)
            {
                var effectSummary = string.IsNullOrWhiteSpace(LastExpeditionEffectMessage)
                    ? string.Empty
                    : $"\nNode Effect: {LastExpeditionEffectMessage}";
                LastBattleSummary = $"{summary}\nRoute: {currentNode.Label}{effectSummary}";
            }
            else
            {
                LastBattleSummary = summary;
            }
        }
        else
        {
            LastBattleSummary = summary;
            if (!IsQuickBattleSmokeActive)
            {
                HasActiveExpeditionRun = false;
            }
        }

        EnsureRewardChoices(reset: true);
    }

    public bool ApplyRewardChoice(int index)
    {
        if (index < 0 || index >= _pendingRewardChoices.Count)
        {
            return false;
        }

        var choice = _pendingRewardChoices[index];
        switch (choice.Kind)
        {
            case RewardChoiceKind.Gold:
                Profile.Currencies.Gold += choice.GoldAmount;
                LastRewardApplicationSummary = $"{choice.Title}: +{choice.GoldAmount} Gold";
                break;
            case RewardChoiceKind.Item:
                Profile.Inventory.Add(new InventoryItemRecord
                {
                    ItemInstanceId = $"{choice.PayloadId}-{Guid.NewGuid():N}",
                    ItemBaseId = choice.PayloadId,
                    EquippedHeroId = string.Empty,
                    AffixIds = new List<string>()
                });
                LastRewardApplicationSummary = $"{choice.Title}: {choice.PayloadId} 획득";
                break;
            case RewardChoiceKind.TemporaryAugment:
                if (!string.IsNullOrWhiteSpace(choice.PayloadId) && !Expedition.TemporaryAugmentIds.Contains(choice.PayloadId))
                {
                    Expedition.AddTemporaryAugment(choice.PayloadId);
                }

                LastRewardApplicationSummary = $"{choice.Title}: run 한정 augment 추가";
                break;
            case RewardChoiceKind.TraitRerollCurrency:
                Profile.Currencies.TraitRerollCurrency += choice.TraitRerollAmount;
                LastRewardApplicationSummary = $"{choice.Title}: Trait Reroll +{choice.TraitRerollAmount}";
                break;
            case RewardChoiceKind.PermanentAugmentSlot:
                GrantPermanentAugmentSlots(choice.PermanentSlotAmount, choice.PayloadId);
                LastRewardApplicationSummary = $"{choice.Title}: Permanent Slot +{choice.PermanentSlotAmount}";
                break;
        }

        Profile.RunSummaries.Add(new RunSummaryRecord
        {
            RunId = Guid.NewGuid().ToString("N"),
            ExpeditionId = IsQuickBattleSmokeActive ? "quick-battle" : $"node-{CurrentExpeditionNodeIndex}",
            Result = LastBattleVictory ? "victory" : "defeat",
            GoldEarned = choice.Kind == RewardChoiceKind.Gold ? choice.GoldAmount : 0,
            NodesCleared = CurrentExpeditionNodeIndex + 1,
            CompletedAtUtc = DateTime.UtcNow.ToString("O")
        });

        _pendingRewardChoices.Clear();
        return true;
    }

    private void EnsureRecruitOffers()
    {
        while (_recruitOffers.Count < 3)
        {
            _recruitOffers.Add(CreateRecruitOffer(_recruitOfferGeneration + Profile.Heroes.Count + _recruitOffers.Count));
        }
    }

    private void EnsureDefaultSquad()
    {
        if (_expeditionSquadHeroIds.Count > 0)
        {
            return;
        }

        foreach (var hero in Profile.Heroes.Take(MetaBalanceDefaults.ExpeditionSquadCap))
        {
            _expeditionSquadHeroIds.Add(hero.HeroId);
        }
    }

    private void EnsureAssignmentMapInitialized()
    {
        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (!_deploymentAssignments.ContainsKey(anchor))
            {
                _deploymentAssignments[anchor] = null;
            }
        }
    }

    private void ResetDeploymentAssignments()
    {
        _deploymentAssignments.Clear();
        EnsureAssignmentMapInitialized();
    }

    private void EnsureDefaultDeploymentAssignments()
    {
        EnsureAssignmentMapInitialized();

        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (_deploymentAssignments.TryGetValue(anchor, out var heroId) && !string.IsNullOrWhiteSpace(heroId) && !_expeditionSquadHeroIds.Contains(heroId))
            {
                _deploymentAssignments[anchor] = null;
            }
        }

        foreach (var heroId in BattleDeployHeroIds.Where(heroId => !_expeditionSquadHeroIds.Contains(heroId)).ToList())
        {
            ClearDeploymentForHero(heroId);
        }

        foreach (var heroId in _expeditionSquadHeroIds.Take(MetaBalanceDefaults.BattleDeployCap))
        {
            if (BattleDeployHeroIds.Contains(heroId))
            {
                continue;
            }

            AssignHeroToAnchor(ResolvePreferredAnchor(heroId), heroId);
            if (BattleDeployHeroIds.Count >= MetaBalanceDefaults.BattleDeployCap)
            {
                break;
            }
        }
    }

    private void ClearDeploymentForHero(string heroId)
    {
        foreach (var anchor in DeploymentAnchorOrder)
        {
            if (_deploymentAssignments.TryGetValue(anchor, out var assignedHero) && assignedHero == heroId)
            {
                _deploymentAssignments[anchor] = null;
            }
        }
    }

    private DeploymentAnchorId ResolvePreferredAnchor(string heroId)
    {
        var hero = Profile.Heroes.FirstOrDefault(entry => entry.HeroId == heroId);
        var preferredOrder = hero?.ClassId switch
        {
            "vanguard" => new[]
            {
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom
            },
            "duelist" => new[]
            {
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.BackCenter
            },
            "ranger" => new[]
            {
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontBottom
            },
            "mystic" => new[]
            {
                DeploymentAnchorId.BackCenter,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom
            },
            _ => DeploymentAnchorOrder
        };

        foreach (var anchor in preferredOrder)
        {
            if (string.IsNullOrWhiteSpace(GetAssignedHeroId(anchor)))
            {
                return anchor;
            }
        }

        return preferredOrder[0];
    }

    private void EnsureExpeditionNodes(bool reset = false)
    {
        if (reset)
        {
            _expeditionNodes.Clear();
        }

        if (_expeditionNodes.Count > 0)
        {
            return;
        }

        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            0,
            "camp",
            "출발 Camp",
            "좌/우 분기 선택",
            "좌측 Gold route 또는 우측 utility route를 선택합니다.",
            false,
            ExpeditionNodeEffectKind.None,
            0,
            string.Empty,
            new[] { 1, 2 }));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            1,
            "ambush-route",
            "Ambush Route",
            "Gold Spike",
            "전투 승리 시 즉시 +4 Gold를 얻는 공격적 경로.",
            true,
            ExpeditionNodeEffectKind.Gold,
            4,
            string.Empty,
            new[] { 3 }));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            2,
            "relay-route",
            "Scout Relay",
            "Trait Reroll",
            "전투 승리 시 Trait Reroll +1을 얻는 안정 경로.",
            true,
            ExpeditionNodeEffectKind.TraitRerollCurrency,
            1,
            string.Empty,
            new[] { 3 }));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            3,
            "shrine-route",
            "Shrine Clash",
            "Temp Augment",
            "승리 시 이번 run에만 적용되는 임시 augment를 추가합니다.",
            true,
            ExpeditionNodeEffectKind.TemporaryAugment,
            0,
            ResolveRewardAugmentId(2),
            new[] { 4 }));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(
            4,
            "extract-route",
            "Extract",
            "Safe Cashout",
            "전투 없이 정리 보급을 받아 Town으로 복귀할 수 있습니다.",
            false,
            ExpeditionNodeEffectKind.Gold,
            4,
            string.Empty,
            Array.Empty<int>()));
    }

    private void EnsureRewardChoices(bool reset = false)
    {
        if (reset)
        {
            _pendingRewardChoices.Clear();
        }

        if (_pendingRewardChoices.Count > 0)
        {
            return;
        }

        foreach (var choice in BuildRewardChoicesForCurrentContext())
        {
            _pendingRewardChoices.Add(choice);
        }
    }

    private IEnumerable<RewardChoiceViewModel> BuildRewardChoicesForCurrentContext()
    {
        if (!LastBattleVictory)
        {
            return new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "Fallback Stash", "패배 보정: +3 Gold", 3, 0, 0, string.Empty),
                new RewardChoiceViewModel(RewardChoiceKind.TraitRerollCurrency, "Tactical Notes", "패배 보정: Trait Reroll +1", 0, 1, 0, string.Empty),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Guard Spark", "다음 run 전까지 방어 템포를 보정하는 temp augment", 0, 0, 0, ResolveRewardAugmentId(0))
            };
        }

        if (IsQuickBattleSmokeActive)
        {
            return new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "Gold Cache", "+5 Gold", 5, 0, 0, string.Empty),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Iron Blade", "Base item 1개", 0, 0, 0, ResolveRewardItemId(0)),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Aggro Spark", "이번 run 동안 공격 템포 + 보정", 0, 0, 0, ResolveRewardAugmentId(1))
            };
        }

        return GetCurrentExpeditionNode()?.Id switch
        {
            "ambush-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "War Chest", "공격 경로 보정: +8 Gold", 8, 0, 0, string.Empty),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Hook Spear", "전열용 기본 아이템 1개", 0, 0, 0, ResolveRewardItemId(1)),
                new RewardChoiceViewModel(RewardChoiceKind.TraitRerollCurrency, "Scout Intel", "다음 채용 정리에 쓸 Trait Reroll +1", 0, 1, 0, string.Empty)
            },
            "relay-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Field Kit", "유틸 아이템 1개", 0, 0, 0, ResolveRewardItemId(2)),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Anchor Beat", "이번 run 동안 회복/방어 안정화 temp augment", 0, 0, 0, ResolveRewardAugmentId(2)),
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "Relay Pouch", "보급품 환전 +6 Gold", 6, 0, 0, string.Empty)
            },
            "shrine-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.PermanentAugmentSlot, "Permanent Socket", "meta progression: Permanent Slot +1", 0, 0, 1, "perm-slot-shrine"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Sigil Core", "후열용 아이템 1개", 0, 0, 0, ResolveRewardItemId(3)),
                new RewardChoiceViewModel(RewardChoiceKind.TraitRerollCurrency, "Doctrine Cache", "meta 정리용 Trait Reroll +2", 0, 2, 0, string.Empty)
            },
            _ => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "Gold Cache", "+5 Gold", 5, 0, 0, string.Empty),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Iron Blade", "Base item 1개", 0, 0, 0, ResolveRewardItemId(0)),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Aggro Spark", "이번 run 동안 공격 템포 + 보정", 0, 0, 0, ResolveRewardAugmentId(1))
            }
        };
    }

    private string ApplyExpeditionNodeEffect(ExpeditionNodeViewModel node)
    {
        return node.EffectKind switch
        {
            ExpeditionNodeEffectKind.None => $"{node.Label}: 분기 선택 대기",
            ExpeditionNodeEffectKind.Gold => ApplyGoldNodeEffect(node),
            ExpeditionNodeEffectKind.TraitRerollCurrency => ApplyTraitRerollNodeEffect(node),
            ExpeditionNodeEffectKind.TemporaryAugment => ApplyTemporaryAugmentNodeEffect(node),
            ExpeditionNodeEffectKind.PermanentAugmentSlot => ApplyPermanentSlotNodeEffect(node),
            _ => $"{node.Label}: 효과 없음"
        };
    }

    private string ApplyGoldNodeEffect(ExpeditionNodeViewModel node)
    {
        Profile.Currencies.Gold += node.EffectAmount;
        return $"{node.Label}: +{node.EffectAmount} Gold";
    }

    private string ApplyTraitRerollNodeEffect(ExpeditionNodeViewModel node)
    {
        Profile.Currencies.TraitRerollCurrency += node.EffectAmount;
        return $"{node.Label}: Trait Reroll +{node.EffectAmount}";
    }

    private string ApplyTemporaryAugmentNodeEffect(ExpeditionNodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.EffectPayloadId) && !Expedition.TemporaryAugmentIds.Contains(node.EffectPayloadId))
        {
            Expedition.AddTemporaryAugment(node.EffectPayloadId);
        }

        return $"{node.Label}: temp augment '{node.EffectPayloadId}' 적용";
    }

    private string ApplyPermanentSlotNodeEffect(ExpeditionNodeViewModel node)
    {
        GrantPermanentAugmentSlots(Math.Max(1, node.EffectAmount), node.EffectPayloadId);
        return $"{node.Label}: Permanent Slot +{Math.Max(1, node.EffectAmount)}";
    }

    private void GrantPermanentAugmentSlots(int amount, string payloadId)
    {
        for (var i = 0; i < Math.Max(1, amount); i++)
        {
            var baseId = string.IsNullOrWhiteSpace(payloadId) ? "perm-slot" : payloadId;
            var nextId = $"{baseId}-{Profile.UnlockedPermanentAugmentIds.Count + 1}";
            while (Profile.UnlockedPermanentAugmentIds.Contains(nextId))
            {
                nextId = $"{baseId}-{Guid.NewGuid():N}";
            }

            Profile.UnlockedPermanentAugmentIds.Add(nextId);
        }

        PermanentAugmentSlotCount = Math.Max(1, Profile.UnlockedPermanentAugmentIds.Count);
    }

    private void MarkCurrentNodeResolved()
    {
        var current = GetCurrentExpeditionNode();
        if (current != null)
        {
            _resolvedExpeditionNodeIds.Add(current.Id);
        }
    }

    private void AutoSelectNextExpeditionNode()
    {
        var nextNodes = GetSelectableNextNodeIndices();
        SelectedExpeditionNodeIndex = nextNodes.Count > 0 ? nextNodes[0] : null;
    }

    private void SyncExpeditionState()
    {
        Expedition = new ExpeditionState(CurrentExpeditionNodeIndex, Expedition.TemporaryAugmentIds);
    }

    private void SeedDemoProfile()
    {
        Profile.DisplayName = "Demo Player";
        Profile.Currencies = new CurrencyRecord { Gold = 10, TraitRerollCurrency = 1 };
        Profile.UnlockedPermanentAugmentIds = new List<string> { "perm-slot-1" };
        Profile.Inventory = new List<InventoryItemRecord>();
        Profile.Heroes.Clear();

        var archetypeIds = _combatContentLookup.GetCanonicalArchetypeIds();
        var itemIds = _combatContentLookup.GetCanonicalItemIds();
        var affixIds = _combatContentLookup.GetCanonicalAffixIds();
        for (var i = 0; i < Math.Min(MetaBalanceDefaults.ExpeditionSquadCap, archetypeIds.Count); i++)
        {
            var archetypeId = archetypeIds[i];
            _combatContentLookup.TryGetArchetype(archetypeId, out var archetype);
            var heroId = $"hero-{i + 1}";
            var equippedItems = new List<string>();
            if (itemIds.Count > 0 && i < 4)
            {
                var itemInstanceId = $"demo-item-{i + 1}";
                Profile.Inventory.Add(new InventoryItemRecord
                {
                    ItemInstanceId = itemInstanceId,
                    ItemBaseId = itemIds[i % itemIds.Count],
                    EquippedHeroId = heroId,
                    AffixIds = affixIds.Count == 0
                        ? new List<string>()
                        : new List<string> { affixIds[i % affixIds.Count] }
                });
                equippedItems.Add(itemInstanceId);
            }

            Profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = heroId,
                Name = $"Hero {i + 1}",
                ArchetypeId = archetypeId,
                RaceId = archetype?.Race.Id ?? string.Empty,
                ClassId = archetype?.Class.Id ?? string.Empty,
                PositiveTraitId = _combatContentLookup.NormalizePositiveTraitId(archetypeId, string.Empty, i),
                NegativeTraitId = _combatContentLookup.NormalizeNegativeTraitId(archetypeId, string.Empty, i + 1),
                EquippedItemIds = equippedItems
            });
        }
    }

    private IReadOnlyList<BattleEquippedItemSpec> BuildEquippedItemSpecs(
        HeroInstanceRecord hero,
        IReadOnlyDictionary<string, InventoryItemRecord> inventoryByInstanceId)
    {
        var instanceIds = new HashSet<string>(hero.EquippedItemIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        foreach (var inventoryItem in Profile.Inventory.Where(item => item.EquippedHeroId == hero.HeroId))
        {
            instanceIds.Add(inventoryItem.ItemInstanceId);
        }

        return instanceIds
            .Where(inventoryByInstanceId.ContainsKey)
            .Select(id => inventoryByInstanceId[id])
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemBaseId))
            .Select(item => new BattleEquippedItemSpec(
                item.ItemBaseId,
                item.AffixIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }

    private void NormalizeProfileContentIds()
    {
        NormalizeHeroContentIds();
        NormalizeInventoryContentIds();
        NormalizeExpeditionContentIds();
        NormalizeEquippedItemReferences();
    }

    private void NormalizeHeroContentIds()
    {
        for (var i = 0; i < Profile.Heroes.Count; i++)
        {
            var hero = Profile.Heroes[i];
            hero.EquippedItemIds ??= new List<string>();
            hero.ArchetypeId = _combatContentLookup.NormalizeArchetypeId(hero.ArchetypeId, hero.RaceId, hero.ClassId, i);
            if (_combatContentLookup.TryGetArchetype(hero.ArchetypeId, out var archetype))
            {
                hero.RaceId = archetype.Race.Id;
                hero.ClassId = archetype.Class.Id;
            }

            hero.PositiveTraitId = _combatContentLookup.NormalizePositiveTraitId(hero.ArchetypeId, hero.PositiveTraitId, i);
            hero.NegativeTraitId = _combatContentLookup.NormalizeNegativeTraitId(hero.ArchetypeId, hero.NegativeTraitId, i + 1);
            hero.EquippedItemIds = hero.EquippedItemIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private void NormalizeInventoryContentIds()
    {
        for (var i = 0; i < Profile.Inventory.Count; i++)
        {
            var item = Profile.Inventory[i];
            item.AffixIds ??= new List<string>();
            if (string.IsNullOrWhiteSpace(item.ItemInstanceId))
            {
                item.ItemInstanceId = $"inventory-{Guid.NewGuid():N}";
            }

            item.ItemBaseId = _combatContentLookup.NormalizeItemBaseId(item.ItemBaseId, i);
            item.AffixIds = item.AffixIds
                .Select((affixId, affixIndex) => _combatContentLookup.NormalizeAffixId(affixId, i + affixIndex))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    private void NormalizeExpeditionContentIds()
    {
        Expedition = new ExpeditionState(
            Expedition.CurrentNodeIndex,
            Expedition.TemporaryAugmentIds
                .Select((augmentId, index) => _combatContentLookup.NormalizeTemporaryAugmentId(augmentId, index))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList());
    }

    private void NormalizeEquippedItemReferences()
    {
        var inventoryById = Profile.Inventory
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemInstanceId))
            .GroupBy(item => item.ItemInstanceId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var heroIds = Profile.Heroes.Select(hero => hero.HeroId).ToHashSet(StringComparer.Ordinal);

        foreach (var inventoryItem in Profile.Inventory)
        {
            if (!string.IsNullOrWhiteSpace(inventoryItem.EquippedHeroId) && !heroIds.Contains(inventoryItem.EquippedHeroId))
            {
                inventoryItem.EquippedHeroId = string.Empty;
            }
        }

        foreach (var hero in Profile.Heroes)
        {
            var equippedIds = new HashSet<string>(hero.EquippedItemIds.Where(inventoryById.ContainsKey), StringComparer.Ordinal);
            foreach (var inventoryItem in Profile.Inventory.Where(item => item.EquippedHeroId == hero.HeroId))
            {
                equippedIds.Add(inventoryItem.ItemInstanceId);
            }

            hero.EquippedItemIds = equippedIds.ToList();
            foreach (var equippedId in equippedIds)
            {
                inventoryById[equippedId].EquippedHeroId = hero.HeroId;
            }
        }
    }

    private RecruitOffer CreateRecruitOffer(int offerSeed)
    {
        var archetypeId = _combatContentLookup.NormalizeArchetypeId(string.Empty, string.Empty, string.Empty, offerSeed);
        _combatContentLookup.TryGetArchetype(archetypeId, out var archetype);
        var offerIndex = offerSeed + 1;
        return new RecruitOffer(
            $"offer-hero-{offerIndex}",
            $"Recruit {offerIndex}",
            archetypeId,
            archetype?.Race.Id ?? string.Empty,
            archetype?.Class.Id ?? string.Empty,
            _combatContentLookup.NormalizePositiveTraitId(archetypeId, string.Empty, offerSeed),
            _combatContentLookup.NormalizeNegativeTraitId(archetypeId, string.Empty, offerSeed + 1));
    }

    private string ResolveRewardItemId(int index)
    {
        return _combatContentLookup.NormalizeItemBaseId(string.Empty, index);
    }

    private string ResolveRewardAugmentId(int index)
    {
        return _combatContentLookup.NormalizeTemporaryAugmentId(string.Empty, index);
    }

    private static IEnumerable<HeroRecord> ToHeroRecords(SaveProfile profile)
    {
        foreach (var hero in profile.Heroes)
        {
            yield return new HeroRecord(
                hero.HeroId,
                hero.Name,
                hero.ArchetypeId,
                hero.RaceId,
                hero.ClassId,
                hero.PositiveTraitId,
                hero.NegativeTraitId,
                Array.Empty<MetaModifierPackage>());
        }
    }
}

public sealed record RecruitOffer(
    string HeroId,
    string Name,
    string ArchetypeId,
    string RaceId,
    string ClassId,
    string PositiveTraitId,
    string NegativeTraitId);

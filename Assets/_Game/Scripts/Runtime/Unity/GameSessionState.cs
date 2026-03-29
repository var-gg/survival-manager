using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

public sealed class GameSessionState
{
    private readonly List<string> _expeditionSquadHeroIds = new();
    private readonly List<string> _battleDeployHeroIds = new();
    private readonly List<RecruitOffer> _recruitOffers = new();
    private readonly List<ExpeditionNodeViewModel> _expeditionNodes = new();
    private readonly List<RewardChoiceViewModel> _pendingRewardChoices = new();
    private readonly HashSet<string> _resolvedExpeditionNodeIds = new(StringComparer.Ordinal);

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
    public IReadOnlyList<string> ExpeditionSquadHeroIds => _expeditionSquadHeroIds;
    public IReadOnlyList<string> BattleDeployHeroIds => _battleDeployHeroIds;
    public IReadOnlyList<RecruitOffer> RecruitOffers => _recruitOffers;
    public IReadOnlyList<ExpeditionNodeViewModel> ExpeditionNodes => _expeditionNodes;
    public IReadOnlyList<RewardChoiceViewModel> PendingRewardChoices => _pendingRewardChoices;
    public bool CanResumeExpedition => HasActiveExpeditionRun && !IsQuickBattleSmokeActive && CurrentExpeditionNodeIndex < _expeditionNodes.Count - 1;

    public void BindProfile(SaveProfile profile)
    {
        Profile = profile;

        if (Profile.Heroes.Count == 0)
        {
            SeedDemoProfile();
        }

        Profile.Currencies ??= new CurrencyRecord();
        Profile.Inventory ??= new List<InventoryItemRecord>();
        Profile.UnlockedPermanentAugmentIds ??= new List<string>();
        Profile.RunSummaries ??= new List<RunSummaryRecord>();

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
        _resolvedExpeditionNodeIds.Clear();

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

    public void RerollRecruitOffers()
    {
        if (Profile.Currencies.Gold > 0)
        {
            Profile.Currencies.Gold = Math.Max(0, Profile.Currencies.Gold - 1);
        }

        _recruitOffers.Clear();
        EnsureRecruitOffers();
    }

    public bool Recruit(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= _recruitOffers.Count)
        {
            return false;
        }

        var offer = _recruitOffers[offerIndex];
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
        return true;
    }

    public bool ToggleExpeditionHero(string heroId)
    {
        if (_expeditionSquadHeroIds.Contains(heroId))
        {
            _expeditionSquadHeroIds.Remove(heroId);
            _battleDeployHeroIds.Remove(heroId);
            RefillBattleDeployFromSquad();
            return true;
        }

        if (_expeditionSquadHeroIds.Count >= 8)
        {
            return false;
        }

        _expeditionSquadHeroIds.Add(heroId);
        if (_battleDeployHeroIds.Count < 4)
        {
            _battleDeployHeroIds.Add(heroId);
        }
        return true;
    }

    public void EnsureBattleDeployReady()
    {
        EnsureDefaultSquad();
        RefillBattleDeployFromSquad();
    }

    public void PromoteToBattleDeploy(string heroId)
    {
        if (!_expeditionSquadHeroIds.Contains(heroId))
        {
            return;
        }

        _battleDeployHeroIds.Remove(heroId);
        _battleDeployHeroIds.Insert(0, heroId);

        while (_battleDeployHeroIds.Count > 4)
        {
            _battleDeployHeroIds.RemoveAt(_battleDeployHeroIds.Count - 1);
        }

        RefillBattleDeployFromSquad();
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
            var index = Profile.Heroes.Count + _recruitOffers.Count + 1;
            _recruitOffers.Add(new RecruitOffer(
                $"offer-{index}",
                $"Recruit {index}",
                $"archetype-{((index - 1) % 8) + 1}",
                index % 3 == 1 ? "human" : index % 3 == 2 ? "beastkin" : "undead",
                index % 4 == 1 ? "vanguard" : index % 4 == 2 ? "duelist" : index % 4 == 3 ? "ranger" : "mystic",
                $"positive-trait-{((index - 1) % 6) + 1}",
                $"negative-trait-{((index - 1) % 6) + 1}"));
        }
    }

    private void EnsureDefaultSquad()
    {
        if (_expeditionSquadHeroIds.Count > 0)
        {
            return;
        }

        foreach (var hero in Profile.Heroes.Take(8))
        {
            _expeditionSquadHeroIds.Add(hero.HeroId);
        }

        RefillBattleDeployFromSquad();
    }

    private void RefillBattleDeployFromSquad()
    {
        foreach (var heroId in _expeditionSquadHeroIds)
        {
            if (_battleDeployHeroIds.Count >= 4)
            {
                break;
            }

            if (!_battleDeployHeroIds.Contains(heroId))
            {
                _battleDeployHeroIds.Add(heroId);
            }
        }

        _battleDeployHeroIds.RemoveAll(id => !_expeditionSquadHeroIds.Contains(id));
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
            "temp-shrine-beat",
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
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Guard Spark", "다음 run 전까지 방어 템포를 보정하는 temp augment", 0, 0, 0, "temp-guard-spark")
            };
        }

        if (IsQuickBattleSmokeActive)
        {
            return new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "Gold Cache", "+5 Gold", 5, 0, 0, string.Empty),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Iron Blade", "Base item 1개", 0, 0, 0, "item-iron-blade"),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Aggro Spark", "이번 run 동안 공격 템포 + 보정", 0, 0, 0, "temp-aggro-spark")
            };
        }

        return GetCurrentExpeditionNode()?.Id switch
        {
            "ambush-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "War Chest", "공격 경로 보정: +8 Gold", 8, 0, 0, string.Empty),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Hook Spear", "전열용 기본 아이템 1개", 0, 0, 0, "item-hook-spear"),
                new RewardChoiceViewModel(RewardChoiceKind.TraitRerollCurrency, "Scout Intel", "다음 채용 정리에 쓸 Trait Reroll +1", 0, 1, 0, string.Empty)
            },
            "relay-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Field Kit", "유틸 아이템 1개", 0, 0, 0, "item-field-kit"),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Anchor Beat", "이번 run 동안 회복/방어 안정화 temp augment", 0, 0, 0, "temp-anchor-beat"),
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "Relay Pouch", "보급품 환전 +6 Gold", 6, 0, 0, string.Empty)
            },
            "shrine-route" => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.PermanentAugmentSlot, "Permanent Socket", "meta progression: Permanent Slot +1", 0, 0, 1, "perm-slot-shrine"),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Sigil Core", "후열용 아이템 1개", 0, 0, 0, "item-sigil-core"),
                new RewardChoiceViewModel(RewardChoiceKind.TraitRerollCurrency, "Doctrine Cache", "meta 정리용 Trait Reroll +2", 0, 2, 0, string.Empty)
            },
            _ => new[]
            {
                new RewardChoiceViewModel(RewardChoiceKind.Gold, "Gold Cache", "+5 Gold", 5, 0, 0, string.Empty),
                new RewardChoiceViewModel(RewardChoiceKind.Item, "Iron Blade", "Base item 1개", 0, 0, 0, "item-iron-blade"),
                new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Aggro Spark", "이번 run 동안 공격 템포 + 보정", 0, 0, 0, "temp-aggro-spark")
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

        for (var i = 1; i <= 8; i++)
        {
            Profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = $"hero-{i}",
                Name = $"Hero {i}",
                ArchetypeId = $"archetype-{i}",
                RaceId = i % 3 == 1 ? "human" : i % 3 == 2 ? "beastkin" : "undead",
                ClassId = i % 4 == 1 ? "vanguard" : i % 4 == 2 ? "duelist" : i % 4 == 3 ? "ranger" : "mystic",
                PositiveTraitId = $"positive-trait-{((i - 1) % 6) + 1}",
                NegativeTraitId = $"negative-trait-{((i - 1) % 6) + 1}",
                EquippedItemIds = new List<string>()
            });
        }
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

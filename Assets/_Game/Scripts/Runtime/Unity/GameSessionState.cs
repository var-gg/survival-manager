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

    public SaveProfile Profile { get; private set; } = new();
    public RosterState Roster { get; private set; } = new();
    public ExpeditionState Expedition { get; private set; } = new();
    public string CurrentSceneName { get; private set; } = SceneNames.Boot;
    public int PermanentAugmentSlotCount { get; private set; } = 1;
    public int CurrentExpeditionNodeIndex { get; private set; }
    public bool LastBattleVictory { get; private set; }
    public string LastBattleSummary { get; private set; } = string.Empty;
    public IReadOnlyList<string> ExpeditionSquadHeroIds => _expeditionSquadHeroIds;
    public IReadOnlyList<string> BattleDeployHeroIds => _battleDeployHeroIds;
    public IReadOnlyList<RecruitOffer> RecruitOffers => _recruitOffers;
    public IReadOnlyList<ExpeditionNodeViewModel> ExpeditionNodes => _expeditionNodes;
    public IReadOnlyList<RewardChoiceViewModel> PendingRewardChoices => _pendingRewardChoices;

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

        Roster = new RosterState(ToHeroRecords(Profile));
        EnsureRecruitOffers();
        EnsureDefaultSquad();
        EnsureExpeditionNodes();
        EnsureRewardChoices();
        Expedition = new ExpeditionState(CurrentExpeditionNodeIndex);
    }

    public void BeginNewExpedition()
    {
        CurrentExpeditionNodeIndex = 0;
        LastBattleVictory = false;
        LastBattleSummary = string.Empty;
        EnsureExpeditionNodes(reset: true);
        EnsureRewardChoices(reset: true);
        Expedition = new ExpeditionState(CurrentExpeditionNodeIndex);
    }

    public void AdvanceExpeditionNode()
    {
        if (CurrentExpeditionNodeIndex < _expeditionNodes.Count - 1)
        {
            CurrentExpeditionNodeIndex++;
            Expedition.AdvanceNode();
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
                break;
            case RewardChoiceKind.Item:
                Profile.Inventory.Add(new InventoryItemRecord
                {
                    ItemInstanceId = choice.ItemId,
                    ItemBaseId = choice.ItemId,
                    EquippedHeroId = string.Empty,
                    AffixIds = new List<string>()
                });
                break;
            case RewardChoiceKind.TemporaryAugment:
                Expedition.AddTemporaryAugment(choice.TemporaryAugmentId);
                break;
        }

        Profile.RunSummaries.Add(new RunSummaryRecord
        {
            RunId = Guid.NewGuid().ToString("N"),
            ExpeditionId = $"node-{CurrentExpeditionNodeIndex}",
            Result = LastBattleVictory ? "victory" : "defeat",
            GoldEarned = choice.Kind == RewardChoiceKind.Gold ? choice.GoldAmount : 0,
            NodesCleared = CurrentExpeditionNodeIndex + 1,
            CompletedAtUtc = DateTime.UtcNow.ToString("O")
        });

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

        _expeditionNodes.Add(new ExpeditionNodeViewModel(0, "start", "출발", "없음"));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(1, "fork-a", "전투", "Gold"));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(2, "fork-b", "전투", "Item"));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(3, "inner", "전투", "Temporary Augment"));
        _expeditionNodes.Add(new ExpeditionNodeViewModel(4, "exit", "귀환", "Permanent Progress"));
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

        _pendingRewardChoices.Add(new RewardChoiceViewModel(RewardChoiceKind.Gold, "Gold Cache", "+5 Gold", 5, string.Empty, string.Empty));
        _pendingRewardChoices.Add(new RewardChoiceViewModel(RewardChoiceKind.Item, "Iron Blade", "Base item 1개", 0, "item-iron-blade", string.Empty));
        _pendingRewardChoices.Add(new RewardChoiceViewModel(RewardChoiceKind.TemporaryAugment, "Aggro Spark", "이번 run 동안 공격 + 보정", 0, string.Empty, "temp-aggro-spark"));
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

public sealed record ExpeditionNodeViewModel(
    int Index,
    string Id,
    string Label,
    string PlannedReward);

public enum RewardChoiceKind
{
    Gold = 0,
    Item = 1,
    TemporaryAugment = 2
}

public sealed record RewardChoiceViewModel(
    RewardChoiceKind Kind,
    string Title,
    string Description,
    int GoldAmount,
    string ItemId,
    string TemporaryAugmentId);

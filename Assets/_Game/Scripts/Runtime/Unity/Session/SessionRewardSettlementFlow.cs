using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core;
using SM.Core.Contracts;
using SM.Core.Results;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity.Sandbox;
using Unity.Profiling;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    internal sealed class SessionRewardSettlementFlow
    {
        private readonly GameSessionState _session;

        internal SessionRewardSettlementFlow(GameSessionState session)
        {
            _session = session;
        }

        internal void RecordBattleAudit(BattleReplayBundle replay) => _session.RecordBattleAuditCore(replay);

        internal void SetLastBattleResult(bool victory, string summary) =>
            _session.SetLastBattleResultCore(victory, summary);

        internal void MarkBattleResolved(bool victory, int stepCount, int eventCount) =>
            _session.MarkBattleResolvedCore(victory, stepCount, eventCount);

        internal bool ApplyRewardChoice(int index) => _session.ApplyRewardChoiceCore(index);

        internal string PreviewPermanentUnlockFromTemporaryAugment(string augmentId) =>
            _session.PreviewPermanentUnlockFromTemporaryAugmentCore(augmentId);
    }

    private void ApplyLedgerBackedReward(RewardOption option, SessionTextToken summaryToken)
    {
        if (ActiveRun == null)
        {
            ActiveRun = RunStateService.StartRun(IsQuickBattleSmokeActive ? "quick-battle" : GetExpeditionRunId(), CaptureBlueprintState(), IsQuickBattleSmokeActive);
        }

        var currencyState = new CurrencyState(
            Profile.Currencies.Gold,
            Profile.Currencies.Echo,
            Profile.Currencies.TraitRerollCurrency,
            Profile.Currencies.TraitLockToken,
            Profile.Currencies.TraitPurgeToken,
            Profile.Currencies.EmberDust,
            Profile.Currencies.EchoCrystal,
            Profile.Currencies.BossSigil);
        var result = RewardLedgerService.ApplyReward(currencyState, ActiveRun!, option);
        Profile.Currencies.Gold = currencyState.Gold;
        Profile.Currencies.Echo = currencyState.Echo;
        Profile.Currencies.TraitRerollCurrency = currencyState.TraitRerollCurrency;
        Profile.Currencies.TraitLockToken = currencyState.TraitLockToken;
        Profile.Currencies.TraitPurgeToken = currencyState.TraitPurgeToken;
        Profile.Currencies.EmberDust = currencyState.EmberDust;
        Profile.Currencies.EchoCrystal = currencyState.EchoCrystal;
        Profile.Currencies.BossSigil = currencyState.BossSigil;
        Profile.RewardLedger.Add(new RewardLedgerEntryRecord
        {
            EntryId = result.RewardEntry.EntryId,
            RunId = result.RewardEntry.RunId,
            RewardId = result.RewardEntry.RewardId,
            RewardType = result.RewardEntry.RewardType,
            Amount = result.RewardEntry.Amount,
            CreatedAtUtc = result.RewardEntry.CreatedAtUtc,
            Summary = result.RewardEntry.Summary,
            SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
            SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
        });
        if (result.InventoryEntry != null)
        {
            Profile.InventoryLedger.Add(new InventoryLedgerEntryRecord
            {
                EntryId = result.InventoryEntry.EntryId,
                RunId = result.InventoryEntry.RunId,
                ItemInstanceId = result.InventoryEntry.ItemInstanceId,
                ItemBaseId = result.InventoryEntry.ItemBaseId,
                ChangeKind = result.InventoryEntry.ChangeKind,
                Amount = result.InventoryEntry.Amount,
                CreatedAtUtc = result.InventoryEntry.CreatedAtUtc,
                Summary = result.InventoryEntry.Summary,
                SourceId = ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                SourceKind = ResolveRewardSourceKind(ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
            });
        }

        ActiveRun = result.UpdatedRun;
        if (option.Type == SM.Core.Content.RewardType.TemporaryAugment)
        {
            TrackPermanentAugmentProgression(option.Id);
        }

        LastRewardApplicationSummary = summaryToken;
        SyncActiveRunRecord();
        SyncExpeditionState();
    }

    private static string ResolveRoleTag(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private string NormalizeCharacterId(string characterId, string archetypeId)
    {
        if (!string.IsNullOrWhiteSpace(characterId))
        {
            return characterId;
        }

        return string.IsNullOrWhiteSpace(archetypeId) ? string.Empty : archetypeId;
    }

    private string ResolveBlueprintRoleInstructionId(string heroId, string classId, DeploymentAnchorId anchor)
    {
        if (IsQuickBattleSmokeActive && QuickBattleConfig != null && !QuickBattleConfig.UseScenarioAuthoring)
        {
            var overrideId = QuickBattleConfig.AllySlots
                .Where(slot => slot != null && string.Equals(slot.HeroId, heroId, StringComparison.Ordinal))
                .Select(slot => slot.RoleInstructionIdOverride)
                .FirstOrDefault(roleId => !string.IsNullOrWhiteSpace(roleId));
            if (!string.IsNullOrWhiteSpace(overrideId))
            {
                return overrideId;
            }
        }

        return ResolveDefaultRoleInstructionId(classId, anchor);
    }

    private string ResolveSandboxCharacterId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.CharacterId))
        {
            return slot.CharacterId;
        }

        if (!string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride))
        {
            return slot.ArchetypeIdOverride;
        }

        return string.Empty;
    }

    private string ResolveSandboxArchetypeId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride))
        {
            return slot.ArchetypeIdOverride;
        }

        var characterId = ResolveSandboxCharacterId(slot);
        if (_combatContentLookup.TryGetCharacterDefinition(characterId, out var character)
            && character.DefaultArchetype != null)
        {
            return character.DefaultArchetype.Id;
        }

        return characterId;
    }

    private string ResolveSandboxRoleInstructionId(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.RoleInstructionId))
        {
            return slot.RoleInstructionId;
        }

        var characterId = ResolveSandboxCharacterId(slot);
        if (_combatContentLookup.TryGetCharacterDefinition(characterId, out var character)
            && character.DefaultRoleInstruction != null)
        {
            return character.DefaultRoleInstruction.Id;
        }

        var resolvedArchetypeId = ResolveSandboxArchetypeId(slot);
        if (_combatContentLookup.TryGetArchetype(resolvedArchetypeId, out var archetype))
        {
            return ResolveDefaultRoleInstructionId(archetype.Class.Id, slot.Anchor);
        }

        return string.Empty;
    }

    private string ResolveSandboxRoleTag(CombatSandboxEnemySlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.RoleTag) && !string.Equals(slot.RoleTag, "auto", StringComparison.Ordinal))
        {
            return slot.RoleTag;
        }

        var roleInstructionId = ResolveSandboxRoleInstructionId(slot);
        if (_combatContentLookup.TryGetRoleInstructionDefinition(roleInstructionId, out var roleInstruction))
        {
            return roleInstruction.RoleTag;
        }

        var resolvedArchetypeId = ResolveSandboxArchetypeId(slot);
        if (_combatContentLookup.TryGetArchetype(resolvedArchetypeId, out var archetype))
        {
            return ResolveRoleTag(archetype.Class.Id, slot.Anchor);
        }

        return slot.Anchor.IsFrontRow() ? "frontline" : "backline";
    }

    private static string ResolveDefaultRoleInstructionId(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private static SessionTextToken BuildBattleSummaryToken(bool victory, int stepCount, int eventCount)
    {
        return new SessionTextToken(
            GameLocalizationTables.UIReward,
            "ui.reward.battle_summary.base",
            "{0} / {1} steps / {2} events",
            SessionTextArg.Localized(
                GameLocalizationTables.UIReward,
                victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                victory ? "Victory" : "Defeat"),
            SessionTextArg.Number(stepCount),
            SessionTextArg.Number(eventCount));
    }

    private static string BuildRewardChoiceSummaryKey(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => "ui.reward.kind.gold",
            RewardChoiceKind.Item => "ui.reward.kind.item",
            RewardChoiceKind.TemporaryAugment => "ui.reward.kind.temp_augment",
            RewardChoiceKind.Echo => "ui.reward.kind.echo",
            RewardChoiceKind.PermanentAugmentSlot => "ui.reward.kind.permanent_slot",
            _ => "ui.common.none"
        };
    }

    private static SessionTextToken BuildRewardChoiceSummaryToken(RewardChoiceViewModel choice)
    {
        return choice.Kind switch
        {
            RewardChoiceKind.Gold => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.gold",
                "Gold +{0}",
                SessionTextArg.Number(choice.GoldAmount)),
            RewardChoiceKind.Item => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.item",
                "Item / {0}",
                SessionTextArg.ItemName(choice.PayloadId)),
            RewardChoiceKind.TemporaryAugment => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.temp_augment",
                "Temp / {0}",
                SessionTextArg.AugmentName(choice.PayloadId)),
            RewardChoiceKind.Echo => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.echo",
                "Echo +{0}",
                SessionTextArg.Number(choice.EchoAmount)),
            RewardChoiceKind.PermanentAugmentSlot => new SessionTextToken(
                GameLocalizationTables.UIReward,
                "ui.reward.kind.permanent_slot",
                "Permanent Slot +{0}",
                SessionTextArg.Number(choice.PermanentSlotAmount)),
            _ => SessionTextToken.Plain(choice.PayloadId)
        };
    }
}

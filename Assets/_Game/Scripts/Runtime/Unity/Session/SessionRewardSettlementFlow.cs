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

        internal void RecordBattleAudit(BattleReplayBundle replay)
        {
            _session.Profile.MatchHeaders.Add(new MatchRecordHeader
            {
                MatchId = replay.Header.MatchId,
                RunId = _session.ActiveRun?.RunId ?? string.Empty,
                ContentVersion = replay.Header.ContentVersion,
                SimVersion = replay.Header.SimVersion,
                Seed = replay.Header.Seed,
                PlayerSnapshotHash = replay.Header.PlayerSnapshotHash,
                EnemySnapshotHash = replay.Header.EnemySnapshotHash,
                StartedAtUtc = replay.Header.StartedAtUtc,
                CompletedAtUtc = replay.Header.CompletedAtUtc,
                Winner = replay.Header.Winner.ToString(),
                FinalStateHash = replay.Header.FinalStateHash,
            });
            _session.Profile.MatchBlobs.Add(new MatchRecordBlob
            {
                MatchId = replay.Header.MatchId,
                CompileVersion = replay.Input.CompileVersion,
                CompileHash = replay.Input.CompileHash,
                InputDigest = $"{replay.Input.TeamPosture}|{replay.Input.Allies.Count}|{replay.Input.Enemies.Count}",
                BattleSummaryDigest = replay.BattleSummary == null
                    ? string.Empty
                    : $"{replay.BattleSummary.WinnerSideIndex}|{replay.BattleSummary.BattleDurationSeconds:0.###}|{replay.BattleSummary.UnexplainedDamageRatio:0.###}|{replay.BattleSummary.MajorEventCollisionRate:0.###}",
                ReadabilityDigest = replay.Readability == null
                    ? string.Empty
                    : $"{replay.Readability.UnexplainedDamageRatio:0.###}|{replay.Readability.UnexplainedHealingRatio:0.###}|{string.Join(",", replay.Readability.Violations.Select(violation => violation.ToString()))}",
                EventStream = replay.EventStream.Select(@event =>
                    $"{@event.StepIndex}|{@event.ActorId.Value}|{@event.ActionType}|{@event.LogCode}|{@event.TargetId?.Value}|{@event.Value:0.###}|{@event.EventKind}|{@event.PayloadId}|{@event.SecondaryValue:0.###}|{@event.Note}").ToList(),
                KeyframeDigests = replay.Keyframes.Select(frame =>
                    $"{frame.StepIndex}|{frame.TimeSeconds:0.###}|{frame.StateHash}").ToList(),
                TelemetryEvents = replay.TelemetryEvents?
                    .Select(record => $"{record.Domain}|{record.EventKind}|{record.TimeSeconds:0.###}|{record.Explain?.SourceContentId}|{record.Explain?.SourceDisplayName}|{record.StringValueA}|{record.ValueA:0.###}|{record.IntValueA}")
                    .ToList()
                    ?? new List<string>(),
                ArtifactPaths = new List<string>
                {
                    replay.BattleSummary != null ? "Logs/loop-d-balance" : string.Empty,
                    replay.Readability != null ? "Logs/loop-d-balance/readability_watchlist.json" : string.Empty,
                }.Where(path => !string.IsNullOrWhiteSpace(path)).ToList(),
            });

            if (_session.ActiveRun != null)
            {
                if (!string.IsNullOrWhiteSpace(_session.ActiveRun.Overlay.LastCompileHash)
                    && !string.Equals(_session.ActiveRun.Overlay.LastCompileHash, replay.Input.CompileHash, StringComparison.Ordinal))
                {
                    _session.Profile.SuspicionFlags.Add(new SuspicionFlagRecord
                    {
                        FlagId = Guid.NewGuid().ToString("N"),
                        RunId = _session.ActiveRun.RunId,
                        MatchId = replay.Header.MatchId,
                        Reason = "compile_hash_mismatch",
                        ExpectedHash = _session.ActiveRun.Overlay.LastCompileHash,
                        ObservedHash = replay.Input.CompileHash,
                        CreatedAtUtc = DateTime.UtcNow.ToString("O"),
                    });
                }

                _session.ActiveRun = RunStateService.CompleteBattle(_session.ActiveRun, replay.Header.MatchId);
                _session.SyncActiveRunRecord();
            }
        }

        internal void SetLastBattleResult(bool victory, string summary)
        {
            _session.LastBattleVictory = victory;
            _session.LastBattleSummary = SessionTextToken.Plain(summary);
            _session.LastRewardApplicationSummary = SessionTextToken.Empty;
            _session.LastPermanentUnlockSummary = SessionTextToken.Empty;
            _session._hasPendingRewardSettlement = true;
            if (_session.ActiveRun != null)
            {
                _session.ActiveRun = _session.ActiveRun with { LastSettlementWasVictory = victory };
            }

            _session.EnsureRewardChoices(reset: true);
            _session.SyncActiveRunIfPresent();
        }

        internal void MarkBattleResolved(bool victory, int stepCount, int eventCount)
        {
            var resolvedNode = _session.GetSelectedExpeditionNode() ?? _session.GetCurrentExpeditionNode();
            var shouldCreateRewardSettlement = !_session.IsDirectCombatSandboxLane;
            _session.LastBattleVictory = victory;
            _session.LastRewardApplicationSummary = SessionTextToken.Empty;
            _session.LastPermanentUnlockSummary = SessionTextToken.Empty;
            _session._lastAutomaticLootBundle = null;
            _session._hasPendingRewardSettlement = shouldCreateRewardSettlement;

            if (resolvedNode != null && !_session.IsQuickBattleSmokeActive)
            {
                _session.CurrentExpeditionNodeIndex = resolvedNode.Index;
                _session.SelectedExpeditionNodeIndex = resolvedNode.Index;
                _session.EnsureCampaignSelection();
                _session.EnsureActiveRunNodeState(resolvedNode);
            }

            if (victory && _session.ActiveRun != null && !string.IsNullOrWhiteSpace(_session.ActiveRun.Overlay.RewardSourceId))
            {
                _session.TryApplyAutomaticLoot();
            }

            if (victory && resolvedNode != null && !_session.IsQuickBattleSmokeActive)
            {
                _session.MarkNodeResolved(resolvedNode);
                _session.LastExpeditionEffectMessage = _session.ApplyExpeditionNodeEffect(resolvedNode);
                _session.UpdateCampaignProgressForResolvedNode(resolvedNode);
                _session.LastBattleSummary = _session.LastExpeditionEffectMessage.HasValue
                    ? new SessionTextToken(
                        GameLocalizationTables.UIReward,
                        "ui.reward.battle_summary.route_effect",
                        "{0} / {1} steps / {2} events\nRoute: {3}\nNode Effect: {4}",
                        SessionTextArg.Localized(
                            GameLocalizationTables.UIReward,
                            victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                            victory ? "Victory" : "Defeat"),
                        SessionTextArg.Number(stepCount),
                        SessionTextArg.Number(eventCount),
                        SessionTextArg.Localized(GameLocalizationTables.UIExpedition, resolvedNode.LabelKey, resolvedNode.Id),
                        SessionTextArg.Token(_session.LastExpeditionEffectMessage))
                    : new SessionTextToken(
                        GameLocalizationTables.UIReward,
                        "ui.reward.battle_summary.route",
                        "{0} / {1} steps / {2} events\nRoute: {3}",
                        SessionTextArg.Localized(
                            GameLocalizationTables.UIReward,
                            victory ? "ui.reward.result.victory" : "ui.reward.result.defeat",
                            victory ? "Victory" : "Defeat"),
                        SessionTextArg.Number(stepCount),
                        SessionTextArg.Number(eventCount),
                        SessionTextArg.Localized(GameLocalizationTables.UIExpedition, resolvedNode.LabelKey, resolvedNode.Id));
                _session.RestoreResolvedProgressMarkers(includeCurrentNode: true);
            }
            else
            {
                _session.LastBattleSummary = BuildBattleSummaryToken(victory, stepCount, eventCount);
                if (!_session.IsQuickBattleSmokeActive)
                {
                    _session.HasActiveExpeditionRun = false;
                }

                if (resolvedNode != null && !_session.IsQuickBattleSmokeActive)
                {
                    _session.RestoreResolvedProgressMarkers(includeCurrentNode: false);
                }
            }

            if (_session.ActiveRun != null)
            {
                _session.ActiveRun = _session.ActiveRun with
                {
                    LastSettlementWasVictory = victory,
                    Overlay = _session.ActiveRun.Overlay with
                    {
                        PendingRewardIds = shouldCreateRewardSettlement ? _session.ActiveRun.Overlay.PendingRewardIds : Array.Empty<string>(),
                        RewardSourceId = shouldCreateRewardSettlement ? _session.ActiveRun.Overlay.RewardSourceId : string.Empty,
                    }
                };
            }

            if (shouldCreateRewardSettlement)
            {
                _session.EnsureRewardChoices(reset: true);
            }
            else
            {
                _session._pendingRewardChoices.Clear();
            }

            _session.SyncActiveRunIfPresent();
        }

        internal bool ApplyRewardChoice(int index)
        {
            if (index < 0 || index >= _session._pendingRewardChoices.Count)
            {
                return false;
            }

            var choice = _session._pendingRewardChoices[index];
            var rewardSourceId = _session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty;
            if (_session.HasRecordedRewardSettlement(rewardSourceId))
            {
                _session._pendingRewardChoices.Clear();
                _session.LastRewardApplicationSummary = new SessionTextToken(
                    GameLocalizationTables.UIReward,
                    "ui.reward.status.recovered_choice",
                    "Recovered previous reward settlement.");
                _session.AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardSettlementDuplicatePrevented(
                    _session.ResolveTelemetryRunId(),
                    rewardSourceId));
                _session.SyncActiveRunIfPresent();
                return true;
            }

            var timestamp = DateTime.UtcNow.ToString("O");
            switch (choice.Kind)
            {
                case RewardChoiceKind.Gold:
                    _session.ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.Gold, choice.GoldAmount, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                    break;
                case RewardChoiceKind.Item:
                    _session.Profile.Inventory.Add(new InventoryItemRecord
                    {
                        ItemInstanceId = $"{choice.PayloadId}-{Guid.NewGuid():N}",
                        ItemBaseId = choice.PayloadId,
                        EquippedHeroId = string.Empty,
                        AffixIds = new List<string>()
                    });
                    _session.Profile.InventoryLedger.Add(new InventoryLedgerEntryRecord
                    {
                        EntryId = Guid.NewGuid().ToString("N"),
                        RunId = _session.ActiveRun?.RunId ?? string.Empty,
                        ItemInstanceId = _session.Profile.Inventory.Last().ItemInstanceId,
                        ItemBaseId = choice.PayloadId,
                        ChangeKind = "reward_item",
                        Amount = 1,
                        CreatedAtUtc = timestamp,
                        Summary = BuildRewardChoiceSummaryKey(choice),
                        SourceId = _session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                        SourceKind = _session.ResolveRewardSourceKind(_session.ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
                    });
                    _session.Profile.RewardLedger.Add(new RewardLedgerEntryRecord
                    {
                        EntryId = Guid.NewGuid().ToString("N"),
                        RunId = _session.ActiveRun?.RunId ?? string.Empty,
                        RewardId = choice.PayloadId,
                        RewardType = SM.Core.Content.RewardType.Item.ToString(),
                        Amount = 1,
                        CreatedAtUtc = timestamp,
                        Summary = BuildRewardChoiceSummaryKey(choice),
                        SourceId = _session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty,
                        SourceKind = _session.ResolveRewardSourceKind(_session.ActiveRun?.Overlay.RewardSourceId, isSettlementChoice: true),
                    });
                    _session.LastRewardApplicationSummary = BuildRewardChoiceSummaryToken(choice);
                    break;
                case RewardChoiceKind.TemporaryAugment:
                    _session.ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.TemporaryAugment, 1, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                    break;
                case RewardChoiceKind.Echo:
                    _session.ApplyLedgerBackedReward(new RewardOption(choice.PayloadId, SM.Core.Content.RewardType.Echo, choice.EchoAmount, BuildRewardChoiceSummaryKey(choice)), BuildRewardChoiceSummaryToken(choice));
                    break;
                case RewardChoiceKind.PermanentAugmentSlot:
                    return false;
            }

            _session.Profile.RunSummaries.Add(new RunSummaryRecord
            {
                RunId = Guid.NewGuid().ToString("N"),
                ExpeditionId = _session.IsQuickBattleSmokeActive ? "quick-battle" : $"node-{_session.CurrentExpeditionNodeIndex}",
                Result = _session.LastBattleVictory ? "victory" : "defeat",
                GoldEarned = choice.Kind == RewardChoiceKind.Gold ? choice.GoldAmount : 0,
                NodesCleared = _session.CurrentExpeditionNodeIndex + 1,
                CompletedAtUtc = DateTime.UtcNow.ToString("O"),
                ChapterId = _session.ActiveRun?.Overlay.ChapterId ?? _session.Profile.CampaignProgress.SelectedChapterId,
                SiteId = _session.ActiveRun?.Overlay.SiteId ?? _session.Profile.CampaignProgress.SelectedSiteId,
            });

            _session.AppendRuntimeTelemetry(RuntimeOperationalTelemetry.CreateRewardOptionChosen(
                _session.ResolveTelemetryRunId(),
                rewardSourceId,
                choice.PayloadId,
                _session.Profile.Currencies.Gold,
                _session.Profile.Currencies.Echo));
            _session.AppendRuntimeTelemetry(_session.BuildEconomySnapshot("reward_choice_applied"));
            _session._pendingRewardChoices.Clear();
            _session.SyncActiveRunIfPresent();
            return true;
        }

        internal string PreviewPermanentUnlockFromTemporaryAugment(string augmentId)
        {
            if (string.IsNullOrWhiteSpace(augmentId)
                || _session.ActiveRun == null
                || !string.IsNullOrWhiteSpace(_session.ActiveRun.Overlay.FirstSelectedTemporaryAugmentId))
            {
                return string.Empty;
            }

            return _session.ResolvePendingPermanentUnlockId(augmentId);
        }
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

public static class BattleTelemetryRecorder
{
    public static readonly IReadOnlyDictionary<SalienceClass, float> SalienceWeights = new Dictionary<SalienceClass, float>
    {
        [SalienceClass.None] = 0f,
        [SalienceClass.Ambient] = 0.25f,
        [SalienceClass.Minor] = 1f,
        [SalienceClass.Major] = 2f,
        [SalienceClass.Critical] = 3f,
    };

    public static void RecordBattleStarted(BattleState state)
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.BattleStarted,
            TimeSeconds = state.ElapsedSeconds,
        });

        foreach (var unit in state.AllUnits)
        {
            state.AddTelemetry(new TelemetryEventRecord
            {
                Domain = TelemetryDomain.Combat,
                EventKind = unit.EntityKind is CombatEntityKind.RosterUnit
                    ? TelemetryEventKind.UnitSpawned
                    : TelemetryEventKind.SummonSpawned,
                TimeSeconds = state.ElapsedSeconds,
                Actor = BuildEntityRef(unit),
                Explain = BuildSpawnExplain(unit),
                StringValueA = unit.Definition.Name,
            });
        }
    }

    public static void RecordBattleEnded(BattleState state, TeamSide winner, bool timeoutOccurred)
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.BattleEnded,
            TimeSeconds = state.ElapsedSeconds,
            IntValueA = (int)winner,
            BoolValueA = timeoutOccurred,
        });

        RecordActivityTelemetrySnapshot(state);
    }

    public static void RecordActionStarted(BattleState state, UnitSnapshot actor, EvaluatedAction evaluated)
    {
        var explain = BuildActionExplain(actor, evaluated.ActionType, evaluated.Skill, ResolveDecisionReason(actor, evaluated), ResolveActionSalience(evaluated.ActionType, evaluated.Skill));
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = evaluated.ActionType == BattleActionType.BasicAttack ? TelemetryEventKind.BasicAttackStarted : TelemetryEventKind.SkillCastStarted,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(evaluated.Target),
            Explain = explain,
            SkillId = evaluated.Skill?.Id ?? string.Empty,
        });
    }

    public static void RecordActionResolved(BattleState state, UnitSnapshot actor, UnitSnapshot? target, BattleActionType actionType, BattleSkillSpec? skill, float value)
    {
        state.ActivityTelemetry.RecordActionResolved(actor, target, actionType);
        var explain = BuildActionExplain(actor, actionType, skill, actor.PendingDecisionReason, ResolveActionSalience(actionType, skill));
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = actionType == BattleActionType.BasicAttack ? TelemetryEventKind.BasicAttackResolved : TelemetryEventKind.SkillCastResolved,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(target),
            Explain = explain,
            SkillId = skill?.Id ?? string.Empty,
            ValueA = value,
        });
    }

    public static void RecordTargetEvent(BattleState state, UnitSnapshot actor, UnitSnapshot? previousTarget, UnitSnapshot? nextTarget, DecisionReasonCode reasonCode)
    {
        if (nextTarget == null)
        {
            return;
        }

        state.ActivityTelemetry.RecordTargetEvent(state, actor, previousTarget, nextTarget);
        var eventKind = previousTarget == null || string.IsNullOrWhiteSpace(previousTarget.Id.Value)
            ? TelemetryEventKind.TargetAcquired
            : string.Equals(previousTarget.Id.Value, nextTarget.Id.Value, StringComparison.Ordinal)
                ? TelemetryEventKind.TargetAcquired
                : TelemetryEventKind.TargetSwitched;
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = eventKind,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(nextTarget),
            Explain = new ExplainStamp
            {
                SourceKind = ExplainedSourceKind.SystemRule,
                SourceContentId = actor.Definition.Id,
                SourceDisplayName = actor.Definition.Name,
                ReasonCode = reasonCode,
                Salience = eventKind == TelemetryEventKind.TargetSwitched ? SalienceClass.Minor : SalienceClass.Ambient,
            },
        });
    }

    public static void RecordMobility(BattleState state, UnitSnapshot actor, UnitSnapshot? target, MobilityDecision decision)
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.MobilityTriggered,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(target),
            Explain = new ExplainStamp
            {
                SourceKind = ExplainedSourceKind.MobilityReaction,
                SourceContentId = actor.EffectiveMobilityReaction?.Id ?? $"{actor.Definition.Id}:mobility",
                SourceDisplayName = actor.EffectiveMobilityReaction?.Name ?? "Mobility Reaction",
                ReasonCode = actor.PendingDecisionReason,
                Salience = SalienceClass.Major,
            },
            ValueA = decision.Profile.Distance,
            StringValueA = decision.Profile.Purpose.ToString(),
        });
    }

    public static void RecordPositioningIntent(BattleState state, UnitSnapshot actor, UnitSnapshot? target)
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.PositioningIntentUpdated,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(target),
            Explain = new ExplainStamp
            {
                SourceKind = ExplainedSourceKind.SystemRule,
                SourceContentId = actor.Definition.Id,
                SourceDisplayName = actor.Definition.Name,
                ReasonCode = ResolveDecisionReasonFromReplan(actor.PositioningReplanReason),
                Salience = SalienceClass.Ambient,
            },
            IntValueA = actor.PositioningIntentRevision,
            StringValueA = actor.PositioningIntent.ToString(),
            StringValueB = actor.PositioningReplanReason.ToString(),
        });
    }

    public static void RecordPostAttackReposition(BattleState state, UnitSnapshot actor, UnitSnapshot target, float movedDistance, string note)
    {
        state.ActivityTelemetry.RecordPostAttackReposition();
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.PostAttackRepositioned,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(target),
            Explain = new ExplainStamp
            {
                SourceKind = ExplainedSourceKind.SystemRule,
                SourceContentId = state.GetTeamTactic(actor.Side).Id,
                SourceDisplayName = state.GetTeamTactic(actor.Side).DisplayName,
                ReasonCode = DecisionReasonCode.MaintainRangeBand,
                Salience = SalienceClass.Ambient,
            },
            ValueA = movedDistance,
            ValueB = MovementResolver.ComputeEdgeDistance(actor, target),
            StringValueA = note,
        });
    }

    public static void RecordImpact(
        BattleState state,
        TelemetryEventKind eventKind,
        UnitSnapshot actor,
        UnitSnapshot? target,
        BattleActionType actionType,
        BattleSkillSpec? skill,
        float valueA,
        float valueB = 0f,
        string stringValueA = "",
        string stringValueB = "")
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = eventKind,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(target),
            Explain = BuildActionExplain(actor, actionType, skill, actor.PendingDecisionReason, ResolveImpactSalience(eventKind, valueA)),
            SkillId = skill?.Id ?? string.Empty,
            ValueA = valueA,
            ValueB = valueB,
            StringValueA = stringValueA,
            StringValueB = stringValueB,
        });
    }

    public static void RecordStatus(BattleState state, TelemetryEventKind eventKind, UnitSnapshot actor, UnitSnapshot target, string statusId, float value, ExplainedSourceKind sourceKind = ExplainedSourceKind.Status)
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = eventKind,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(target),
            Explain = new ExplainStamp
            {
                SourceKind = sourceKind,
                SourceContentId = ResolveStatusSourceContentId(actor, statusId),
                SourceDisplayName = ResolveStatusDisplayName(actor, statusId),
                ReasonCode = actor.PendingDecisionReason,
                Salience = ResolveStatusSalience(statusId, eventKind),
            },
            StatusId = statusId,
            ValueA = value,
        });
    }

    public static void RecordStatusTick(BattleState state, UnitSnapshot unit, string statusId, float damage)
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.DamageApplied,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(unit),
            Target = BuildEntityRef(unit),
            Explain = new ExplainStamp
            {
                SourceKind = ExplainedSourceKind.Status,
                SourceContentId = statusId,
                SourceDisplayName = statusId,
                ReasonCode = DecisionReasonCode.DefaultCadence,
                Salience = SalienceClass.Minor,
            },
            StatusId = statusId,
            ValueA = damage,
            StringValueA = "status_tick",
        });
    }

    public static void RecordKill(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleActionType actionType, BattleSkillSpec? skill, KillEventPayload? payload)
    {
        var salience = payload != null && payload.IsMirroredFromOwnedSummon ? SalienceClass.Critical : SalienceClass.Major;
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.KillCredited,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Target = BuildEntityRef(target),
            Explain = BuildActionExplain(actor, actionType, skill, DecisionReasonCode.SecureKill, salience),
            SkillId = skill?.Id ?? string.Empty,
            BoolValueA = payload?.IsMirroredFromOwnedSummon ?? false,
        });
    }

    public static void RecordDespawn(BattleState state, UnitSnapshot actor)
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = actor.EntityKind is CombatEntityKind.RosterUnit ? TelemetryEventKind.UnitDied : TelemetryEventKind.SummonDespawned,
            TimeSeconds = state.ElapsedSeconds,
            Actor = BuildEntityRef(actor),
            Explain = BuildSpawnExplain(actor),
            ValueA = actor.Position.X,
            ValueB = actor.Position.Y,
        });
    }

    public static void RecordActivityTelemetrySnapshot(BattleState state)
    {
        var snapshot = state.ActivityTelemetry.BuildSnapshot(state);
        AddMetric(state, "MeanPairwiseDistanceByTeam", snapshot.MeanPairwiseDistanceByTeam.TryGetValue(TeamSide.Ally, out var allyMean) ? allyMean : 0f,
            snapshot.MeanPairwiseDistanceByTeam.TryGetValue(TeamSide.Enemy, out var enemyMean) ? enemyMean : 0f);
        AddMetric(state, "YSpreadStdByRow", 0f, 0f, Serialize(snapshot.YSpreadStdByRow));
        AddMetric(state, "TargetEntropy", snapshot.TargetEntropy);
        AddMetric(state, "FocusHeatPerTarget", 0f, 0f, Serialize(snapshot.FocusHeatPerTarget));
        AddMetric(state, "OverfocusEvents", snapshot.OverfocusEvents);
        AddMetric(state, "TankAbsorbedFocusHeat", snapshot.TankAbsorbedFocusHeat);
        AddMetric(state, "StationaryBetweenAttacksRatio", snapshot.StationaryBetweenAttacksRatio);
        AddMetric(state, "PostAttackRepositionCount", snapshot.PostAttackRepositionCount);
        AddMetric(state, "TargetSwitchCount", snapshot.TargetSwitchCount);
        AddMetric(state, "ReplayHash", 0f, 0f, snapshot.ReplayHash);
    }

    public static TelemetryEntityRef? BuildEntityRef(UnitSnapshot? unit)
    {
        if (unit == null)
        {
            return null;
        }

        return new TelemetryEntityRef
        {
            UnitInstanceId = unit.Id.Value,
            UnitBlueprintId = unit.Definition.Id,
            OwnerUnitInstanceId = unit.Ownership?.OwnerEntity.Value ?? string.Empty,
            IsSummon = unit.EntityKind == CombatEntityKind.OwnedSummon,
            IsDeployable = unit.EntityKind == CombatEntityKind.Deployable,
            SideIndex = unit.Side == TeamSide.Ally ? 0 : 1,
        };
    }

    public static DecisionReasonCode ResolveDecisionReason(UnitSnapshot actor, EvaluatedAction evaluated)
    {
        if (evaluated.Mobility != null)
        {
            return evaluated.Mobility.Profile.Purpose switch
            {
                MobilityPurpose.Disengage or MobilityPurpose.Evade or MobilityPurpose.MaintainRange => DecisionReasonCode.EscapeThreat,
                _ => DecisionReasonCode.TriggeredReaction,
            };
        }

        if (evaluated.Target != null && evaluated.Target.HasStatus("guarded"))
        {
            return DecisionReasonCode.BreakGuard;
        }

        if (evaluated.Target != null && evaluated.Target.HasStatus("exposed"))
        {
            return DecisionReasonCode.PunishExposedBackline;
        }

        if (evaluated.ActionType == BattleActionType.ActiveSkill && evaluated.Skill?.UsesEnergy == true)
        {
            return DecisionReasonCode.SpendEnergyWindow;
        }

        if (evaluated.Target != null && evaluated.Target.IsStunned)
        {
            return DecisionReasonCode.BurstDuringCrowdControl;
        }

        if (evaluated.PositioningIntent is PositioningIntentKind.FlankLeft
            or PositioningIntentKind.FlankRight
            or PositioningIntentKind.BacklineDive)
        {
            return DecisionReasonCode.PunishExposedBackline;
        }

        if (actor.Behavior.RangeDiscipline is RangeDiscipline.HoldBand or RangeDiscipline.KiteBackward)
        {
            return DecisionReasonCode.MaintainRangeBand;
        }

        return DecisionReasonCode.DefaultCadence;
    }

    public static float GetSalienceWeight(SalienceClass salience)
    {
        return SalienceWeights.TryGetValue(salience, out var weight) ? weight : 0f;
    }

    private static void AddMetric(BattleState state, string metricId, float valueA, float valueB = 0f, string valueText = "")
    {
        state.AddTelemetry(new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Combat,
            EventKind = TelemetryEventKind.ActivityMetricRecorded,
            TimeSeconds = state.ElapsedSeconds,
            Explain = new ExplainStamp
            {
                SourceKind = ExplainedSourceKind.SystemRule,
                SourceContentId = "activity_telemetry_v1",
                SourceDisplayName = "Activity Telemetry V1",
                ReasonCode = DecisionReasonCode.DefaultCadence,
                Salience = SalienceClass.Ambient,
            },
            ValueA = valueA,
            ValueB = valueB,
            StringValueA = metricId,
            StringValueB = valueText,
        });
    }

    private static string Serialize<TKey>(IReadOnlyDictionary<TKey, float> values)
        where TKey : notnull
    {
        return string.Join(";", values
            .OrderBy(pair => Convert.ToString(pair.Key, CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .Select(pair => $"{Convert.ToString(pair.Key, CultureInfo.InvariantCulture)}={pair.Value.ToString("0.###", CultureInfo.InvariantCulture)}"));
    }

    private static ExplainStamp BuildActionExplain(UnitSnapshot actor, BattleActionType actionType, BattleSkillSpec? skill, DecisionReasonCode reasonCode, SalienceClass salience)
    {
        return new ExplainStamp
        {
            SourceKind = ResolveSourceKind(actor, actionType, skill),
            SourceContentId = actionType == BattleActionType.BasicAttack ? actor.EffectiveBasicAttack.Id : skill?.Id ?? string.Empty,
            SourceDisplayName = actionType == BattleActionType.BasicAttack ? actor.EffectiveBasicAttack.Name : skill?.Name ?? "Active Skill",
            ReasonCode = reasonCode,
            Salience = salience,
        };
    }

    private static DecisionReasonCode ResolveDecisionReasonFromReplan(ReevaluationReason reason)
    {
        return reason switch
        {
            ReevaluationReason.TookHit or ReevaluationReason.RangeBreak or ReevaluationReason.TargetMoved => DecisionReasonCode.MaintainRangeBand,
            ReevaluationReason.TargetLost or ReevaluationReason.SlotLost => DecisionReasonCode.CounterThreatLane,
            _ => DecisionReasonCode.DefaultCadence,
        };
    }

    private static ExplainStamp BuildSpawnExplain(UnitSnapshot actor)
    {
        return new ExplainStamp
        {
            SourceKind = actor.EntityKind is CombatEntityKind.OwnedSummon or CombatEntityKind.Deployable
                ? ExplainedSourceKind.SystemRule
                : ExplainedSourceKind.SystemRule,
            SourceContentId = actor.Definition.Id,
            SourceDisplayName = actor.Definition.Name,
            ReasonCode = actor.EntityKind is CombatEntityKind.OwnedSummon or CombatEntityKind.Deployable
                ? DecisionReasonCode.SummonLifecycle
                : DecisionReasonCode.DefaultCadence,
            Salience = actor.EntityKind is CombatEntityKind.OwnedSummon or CombatEntityKind.Deployable
                ? SalienceClass.Major
                : SalienceClass.Ambient,
        };
    }

    private static ExplainedSourceKind ResolveSourceKind(UnitSnapshot actor, BattleActionType actionType, BattleSkillSpec? skill)
    {
        if (actionType == BattleActionType.BasicAttack)
        {
            return actor.EntityKind is CombatEntityKind.OwnedSummon or CombatEntityKind.Deployable
                ? ExplainedSourceKind.SummonAttack
                : ExplainedSourceKind.BasicAttack;
        }

        if (skill?.EffectiveSlotKind == ActionSlotKind.SignatureActive)
        {
            return actor.EntityKind is CombatEntityKind.OwnedSummon or CombatEntityKind.Deployable
                ? ExplainedSourceKind.SummonSkill
                : ExplainedSourceKind.SignatureActive;
        }

        return skill?.EffectiveSlotKind == ActionSlotKind.FlexActive
            ? ExplainedSourceKind.FlexActive
            : ExplainedSourceKind.SystemRule;
    }

    private static SalienceClass ResolveActionSalience(BattleActionType actionType, BattleSkillSpec? skill)
    {
        if (actionType == BattleActionType.BasicAttack)
        {
            return SalienceClass.Minor;
        }

        return skill?.EffectiveSlotKind == ActionSlotKind.SignatureActive ? SalienceClass.Major : SalienceClass.Major;
    }

    private static SalienceClass ResolveImpactSalience(TelemetryEventKind eventKind, float value)
    {
        return eventKind switch
        {
            TelemetryEventKind.DamageApplied when value >= 20f => SalienceClass.Major,
            TelemetryEventKind.HealingApplied when value >= 18f => SalienceClass.Major,
            TelemetryEventKind.BarrierApplied when value >= 18f => SalienceClass.Major,
            TelemetryEventKind.GuardBroken => SalienceClass.Major,
            TelemetryEventKind.InterruptApplied => SalienceClass.Major,
            TelemetryEventKind.DamageApplied or TelemetryEventKind.HealingApplied or TelemetryEventKind.BarrierApplied => SalienceClass.Minor,
            _ => SalienceClass.Ambient,
        };
    }

    private static string ResolveStatusSourceContentId(UnitSnapshot actor, string statusId)
    {
        return statusId switch
        {
            "burn" or "bleed" => statusId,
            _ => actor.PendingSkillId ?? actor.Definition.Id,
        };
    }

    private static string ResolveStatusDisplayName(UnitSnapshot actor, string statusId)
    {
        if (statusId == "burn" || statusId == "bleed")
        {
            return statusId;
        }

        return actor.PendingSkillId ?? actor.Definition.Name;
    }

    private static SalienceClass ResolveStatusSalience(string statusId, TelemetryEventKind eventKind)
    {
        if (eventKind == TelemetryEventKind.StatusRemoved)
        {
            return SalienceClass.Ambient;
        }

        return statusId switch
        {
            "stun" or "root" or "silence" => SalienceClass.Major,
            "guarded" or "unstoppable" or "exposed" or "sunder" or "wound" => SalienceClass.Major,
            _ => SalienceClass.Ambient,
        };
    }
}

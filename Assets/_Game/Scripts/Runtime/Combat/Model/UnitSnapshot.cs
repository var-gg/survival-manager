using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Core.Numerics;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed class UnitSnapshot
{
    private const float SignatureCastThreshold = 100f;
    private const float EnergyPerBasicAttack = 12f;
    private const float EnergyPerDirectHit = 6f;
    private const float EnergyPerKill = 15f;
    private const float EnergyPerAssist = 8f;
    private const float DirectHitEnergyIcdSeconds = 0.35f;
    private const float InterruptedSignatureRefund = 50f;
    private const float AttackSpeedBaseline = 3f;

    private readonly List<AppliedStatusState> _statuses = new();
    private readonly HashSet<string> _firedTriggers = new(StringComparer.Ordinal);
    private readonly float _guardedIncomingDamageDelta;
    private bool _pendingSignatureEnergySpent;

    public UnitSnapshot(
        EntityId id,
        TeamSide side,
        BattleUnitLoadout definition,
        CombatVector2 anchorPosition,
        CombatVector2 spawnPosition,
        CombatStatusRules? statusRules = null)
    {
        Id = id;
        Side = side;
        Definition = definition;
        // guarded 받는피해 delta — 콘텐츠(StatusFamilyDefinition) 튜닝값. 규칙 미주입 레인(레거시
        // 손조립 테스트)은 과거 sim 리터럴(-0.1)과 동일한 기본값으로 떨어진다(결정성 보존).
        _guardedIncomingDamageDelta = statusRules?.ResolveIncomingDamageDelta("guarded") ?? -0.1f;
        Anchor = definition.PreferredAnchor;
        FixedAnchorPosition = SpatialProjection.QuantizeToFixed(anchorPosition);
        FixedPosition = SpatialProjection.QuantizeToFixed(spawnPosition);
        ActionState = CombatActionState.Spawn;

        var modifiers = definition.Packages?.SelectMany(x => x.Modifiers).ToList() ?? new List<StatModifier>();
        Stats = new StatBlock(new Dictionary<StatKey, float>(definition.BaseStats), modifiers);
        Footprint = CombatProfileDefaults.ResolveFootprint(
            definition.Footprint,
            definition.ClassId,
            Math.Max(0.5f, Stats.Get(StatKey.AttackRange)),
            Math.Max(0.1f, Stats.Get(StatKey.CollisionRadius)),
            Math.Max(1f, Stats.Get(StatKey.MaxHealth)));
        Behavior = CombatProfileDefaults.ResolveBehavior(definition.Behavior, definition.ClassId);
        Mobility = CombatProfileDefaults.ResolveMobility(definition.Mobility, definition.ClassId);
        _health = Hp64.FromFloatQuantized(MaxHealth);
        _energy = Resource64.FromFloatQuantized(Math.Clamp(definition.EffectiveEnergy.Starting, 0f, Math.Max(0f, definition.EffectiveEnergy.Max)));
        RequestReevaluation(ReevaluationReason.Cadence);
    }

    public EntityId Id { get; }
    public TeamSide Side { get; }
    public BattleUnitLoadout Definition { get; }
    public DeploymentAnchorId Anchor { get; }
    // Phase 3.2: 공간 권위는 FixedVector2. AnchorPosition/Position은 read-model/presentation/hash용 egress projection
    // (sim·테스트 호출부는 무수정으로 CombatVector2를 계속 읽는다 — 이제 고정소수 권위의 투영값).
    public FixedVector2 FixedAnchorPosition { get; }
    public CombatVector2 AnchorPosition => SpatialProjection.ToReadModelVector2(FixedAnchorPosition);
    public FixedVector2 FixedPosition { get; private set; }
    public CombatVector2 Position => SpatialProjection.ToReadModelVector2(FixedPosition);
    public StatBlock Stats { get; }
    public FootprintProfile Footprint { get; }
    public BehaviorProfile Behavior { get; }
    public MobilityActionProfile? Mobility { get; }
    public CombatActionState ActionState { get; private set; }
    public EntityId? CurrentTargetId { get; private set; }
    public EntityId? PendingTargetId { get; private set; }
    public BattleActionType? PendingActionType { get; private set; }
    public string? PendingSkillId { get; private set; }
    public int ActionTicksRemaining { get; private set; }
    public int ActionTicksTotal { get; private set; }
    // Egress projection(초) — 권위는 위 정수 틱(Phase 2.2c).
    public float ActionTimerRemaining => ActionTicksRemaining * BattleTickMath.TickSeconds;
    public float ActionTimerTotal => ActionTicksTotal * BattleTickMath.TickSeconds;

    /// <summary>Identity of the in-flight action instance (set at accepted BeginWindup, read when the
    /// matching contact resolves or the action is canceled). <see cref="ActionInstanceId.None"/> when no
    /// action is pending. Part of the action choreography seam (C1) — gameplay-inert.</summary>
    public ActionInstanceId PendingActionInstanceId { get; private set; } = ActionInstanceId.None;
    public int PendingWindupStartTick { get; private set; }
    public int PendingContactTick { get; private set; }
    public SkillDelivery PendingActionDelivery { get; private set; } = SkillDelivery.Melee;

    public int CooldownTicksRemaining { get; private set; }
    public int TargetSwitchLockTicksRemaining { get; private set; }
    public int ReevaluationTicksRemaining { get; private set; }
    public ReevaluationReason PendingReevaluationReason { get; private set; }
    public int MobilityCooldownTicksRemaining { get; private set; }
    public int BlockCooldownTicksRemaining { get; private set; }
    public int DirectHitEnergyIcdTicksRemaining { get; private set; }
    // Egress projections(초) — 권위는 위 정수 틱 backing(Phase 2.2c). sim 분기는 틱 필드를 본다.
    public float CooldownRemaining => CooldownTicksRemaining * BattleTickMath.TickSeconds;
    public float TargetSwitchLockRemaining => TargetSwitchLockTicksRemaining * BattleTickMath.TickSeconds;
    public float ReevaluationRemaining => ReevaluationTicksRemaining * BattleTickMath.TickSeconds;
    public float MobilityCooldownRemaining => MobilityCooldownTicksRemaining * BattleTickMath.TickSeconds;
    public float BlockCooldownRemaining => BlockCooldownTicksRemaining * BattleTickMath.TickSeconds;
    public float DirectHitEnergyIcdRemaining => DirectHitEnergyIcdTicksRemaining * BattleTickMath.TickSeconds;
    private Hp64 _health;
    private Hp64 _barrier;
    private Resource64 _energy;

    /// <summary>read-model HP projection. 권위는 Hp64 backing(_health) — Phase 4.2.</summary>
    public float CurrentHealth => _health.ToFloat();
    /// <summary>read-model energy projection. 권위는 Resource64 backing(_energy) — Phase 4.4.</summary>
    public float CurrentEnergy => _energy.ToFloat();
    /// <summary>read-model barrier projection. 권위는 Hp64 backing(_barrier) — Phase 4.2.</summary>
    public float Barrier => _barrier.ToFloat();
    public IReadOnlyList<AppliedStatusState> Statuses => _statuses;
    public ControlResistWindowState? ControlResistWindow { get; private set; }
    public ActionLane PendingLane { get; private set; } = ActionLane.Primary;
    public ActionLockRule PendingLockRule { get; private set; } = ActionLockRule.None;
    public ActionSlotKind? PendingSlotKind { get; private set; }
    public TargetSelector CurrentTargetSelector { get; private set; } = TargetSelector.CurrentTarget;
    public TargetFallbackPolicy CurrentFallbackPolicy { get; private set; } = TargetFallbackPolicy.KeepCurrentIfStillValid;
    public DecisionReasonCode PendingDecisionReason { get; private set; } = DecisionReasonCode.DefaultCadence;
    public PositioningIntentKind PositioningIntent { get; private set; } = PositioningIntentKind.None;
    public ReevaluationReason PositioningReplanReason { get; private set; } = ReevaluationReason.None;
    public int PositioningIntentRevision { get; private set; }

    /// <summary>Phase 1 tactical-brain intent (what this unit is trying to do). Chosen at a low cadence by
    /// RoleBrain and read by the movement/target executor so the fight reads as role-driven drama. Deterministic
    /// and re-derived on replay — never serialized.</summary>
    public CombatIntent CurrentCombatIntent { get; private set; } = CombatIntent.None;

    /// <summary>Phase 1 anti-jitter: the step at/after which RoleBrain may re-decide this unit's intent (per-role
    /// cadence). Role-critical interrupts and hard interrupts bypass it. Absolute step index (never a modulo), so
    /// it reproduces exactly on re-sim; transient, never serialized.</summary>
    public int NextCombatIntentDecisionStep { get; private set; }
    public bool IsAlive => _health.Raw > 0;
    public bool IsDefending { get; private set; }
    public bool NeedsReevaluation => PendingReevaluationReason != ReevaluationReason.None || ReevaluationTicksRemaining <= 0;
    public CombatEntityKind EntityKind => Definition.EntityKind;
    public OwnershipLink? Ownership => Definition.Ownership;
    public SummonProfile? SummonProfile => Definition.SummonProfile;
    public BattleBasicAttackSpec EffectiveBasicAttack => Definition.EffectiveBasicAttack;
    public BattleSkillSpec? EffectiveSignatureActive => Definition.EffectiveSignatureActive;
    public BattleSkillSpec? EffectiveFlexActive => Definition.EffectiveFlexActive;
    public BattlePassiveSpec EffectiveSignaturePassive => Definition.EffectiveSignaturePassive;
    public BattlePassiveSpec EffectiveFlexPassive => Definition.EffectiveFlexPassive;
    public BattleMobilitySpec? EffectiveMobilityReaction => Definition.EffectiveMobilityReaction;
    public float MaxHealth => Stats.Get(StatKey.MaxHealth);
    public float MaxEnergy => Math.Max(0f, Definition.EffectiveEnergy.Max);
    public float Armor => Math.Max(0f, Stats.Get(StatKey.Armor) - GetStatusMagnitude("sunder"));
    public float Resist => Math.Max(0f, Stats.Get(StatKey.Resist) - GetStatusMagnitude("sunder"));
    public float PhysPen => Math.Max(0f, Stats.Get(StatKey.PhysPen));
    public float MagPen => Math.Max(0f, Stats.Get(StatKey.MagPen));
    public float Lifesteal => Math.Max(0f, Stats.Get(StatKey.Lifesteal));
    public float Omnivamp => Math.Max(0f, Stats.Get(StatKey.Omnivamp));
    public float PhysPower => Stats.Get(StatKey.PhysPower);
    public float MagPower => Stats.Get(StatKey.MagPower);
    public float AttackSpeed => Math.Max(0.1f, Stats.Get(StatKey.AttackSpeed) * GetSlowMultiplier());
    public float HealPower => Stats.Get(StatKey.HealPower);
    public float MoveSpeed => Math.Max(0.1f, Stats.Get(StatKey.MoveSpeed) * GetSlowMultiplier());
    public float AttackRange => Math.Max(0.5f, Stats.Get(StatKey.AttackRange));
    public float SkillHaste => Math.Max(0f, Stats.Get(StatKey.SkillHaste));
    public float ManaMax => Stats.Get(StatKey.ManaMax);
    public float ManaGainOnAttack => Stats.Get(StatKey.ManaGainOnAttack);
    public float ManaGainOnHit => Stats.Get(StatKey.ManaGainOnHit);
    public float CooldownRecovery => SkillHaste;
    public float AggroRadius => Math.Max(AttackRange, Stats.Get(StatKey.AggroRadius));
    public FloatRange PreferredRangeBand => Footprint.PreferredRangeBand;
    public float PreferredDistance => Math.Max(0.5f, Stats.Get(StatKey.PreferredDistance) > 0f
        ? Stats.Get(StatKey.PreferredDistance)
        : Definition.PreferredDistance > 0f
            ? Definition.PreferredDistance
            : Footprint.PreferredRangeBand.Midpoint);
    public float ProtectRadius => Math.Max(0f, Definition.ProtectRadius > 0f ? Definition.ProtectRadius : Stats.Get(StatKey.ProtectRadius));
    public float AttackWindup => Math.Max(0.05f, Stats.Get(StatKey.AttackWindup));
    public float CastWindup => Math.Max(0.05f, Stats.Get(StatKey.CastWindup));
    public float ProjectileSpeed => Math.Max(0f, Stats.Get(StatKey.ProjectileSpeed));
    public float CollisionRadius => Math.Max(0.1f, Stats.Get(StatKey.CollisionRadius));
    public float NavigationRadius => Footprint.NavigationRadius;
    public float SeparationRadius => Footprint.SeparationRadius;
    public float CombatReach => Footprint.CombatReach;
    public float HeadAnchorHeight => Footprint.HeadAnchorHeight;
    public float RepositionCooldown => Math.Max(0f, Stats.Get(StatKey.RepositionCooldown));
    public float AuthoredAttackCooldown => Math.Max(0.1f, Stats.Get(StatKey.AttackCooldown));
    public float BasicAttackCooldownScale => Math.Clamp(AttackSpeed / AttackSpeedBaseline, 0.25f, 3f);
    public float AttackCooldown => Math.Max(0.1f, AuthoredAttackCooldown / BasicAttackCooldownScale);
    public float LeashDistance => Math.Max(0.5f, Stats.Get(StatKey.LeashDistance));
    public float TargetSwitchDelay => Math.Max(0f, Stats.Get(StatKey.TargetSwitchDelay));
    public float Attack => PhysPower;
    public float Defense => Armor;
    public float Speed => AttackSpeed;
    public float HealthRatio => MaxHealth <= 0 ? 0 : CurrentHealth / MaxHealth;
    public bool IsStunned => HasStatus("stun");
    public bool IsRooted => HasStatus("root");
    public bool IsSilenced => HasStatus("silence");
    public bool IsSlowed => HasStatus("slow");
    public bool IsUnstoppable => HasStatus("unstoppable");
    public bool IsGuarded => HasStatus("guarded") || IsDefending;
    public float WindupProgress => ActionState == CombatActionState.ExecuteAction && ActionTicksTotal > 0
        ? 1f - ((float)ActionTicksRemaining / ActionTicksTotal)
        : 0f;
    public float RetargetLockRemaining => TargetSwitchLockRemaining;
    public bool IsHardCommitted => PendingLockRule == ActionLockRule.HardCommit
        || (PendingLockRule == ActionLockRule.SoftCommit && WindupProgress >= 0.4f);

    public void AdvanceTick()
    {
        if (ActionTicksRemaining > 0)
        {
            ActionTicksRemaining--;
        }

        if (CooldownTicksRemaining > 0)
        {
            CooldownTicksRemaining--;
            if (CooldownTicksRemaining == 0)
            {
                RequestReevaluation(ReevaluationReason.SkillReady);
            }
        }

        if (MobilityCooldownTicksRemaining > 0)
        {
            MobilityCooldownTicksRemaining--;
            if (MobilityCooldownTicksRemaining == 0 && Mobility is { IsEnabled: true })
            {
                RequestReevaluation(ReevaluationReason.MobilityReady);
            }
        }

        if (BlockCooldownTicksRemaining > 0)
        {
            BlockCooldownTicksRemaining--;
        }

        if (DirectHitEnergyIcdTicksRemaining > 0)
        {
            DirectHitEnergyIcdTicksRemaining--;
        }

        if (TargetSwitchLockTicksRemaining > 0)
        {
            TargetSwitchLockTicksRemaining--;
        }

        if (ReevaluationTicksRemaining > 0)
        {
            ReevaluationTicksRemaining--;
        }

        if (ControlResistWindow is { } controlResist)
        {
            var remaining = controlResist.RemainingTicks - 1;
            ControlResistWindow = remaining > 0
                ? controlResist with { RemainingTicks = remaining }
                : null;
        }
    }

    public void SetPosition(CombatVector2 position)
    {
        FixedPosition = SpatialProjection.QuantizeToFixed(position);
    }

    public void SetPosition(FixedVector2 position)
    {
        FixedPosition = position;
    }

    public void SetActionState(CombatActionState actionState)
    {
        ActionState = actionState;
    }

    public void SetCurrentTarget(EntityId? targetId)
    {
        CurrentTargetId = targetId;
    }

    public void SetTargetDebugState(TargetSelector selector, TargetFallbackPolicy fallbackPolicy)
    {
        CurrentTargetSelector = selector;
        CurrentFallbackPolicy = fallbackPolicy;
    }

    public void SetPendingDecisionReason(DecisionReasonCode reasonCode)
    {
        PendingDecisionReason = reasonCode;
    }

    public void StartRetargetLock(float durationSeconds)
    {
        TargetSwitchLockTicksRemaining = Math.Max(TargetSwitchLockTicksRemaining, BattleTickMath.DurationToTicks(durationSeconds));
    }

    public void ClearTarget(bool applySwitchDelay)
    {
        var hadTarget = CurrentTargetId != null || PendingTargetId != null;
        var shouldRefundSignature = PendingActionType == BattleActionType.ActiveSkill && _pendingSignatureEnergySpent;
        CurrentTargetId = null;
        PendingTargetId = null;
        PendingActionType = null;
        PendingSkillId = null;
        PendingLane = ActionLane.Primary;
        PendingLockRule = ActionLockRule.None;
        PendingSlotKind = null;
        PendingDecisionReason = DecisionReasonCode.DefaultCadence;
        ActionTicksRemaining = 0;
        ActionTicksTotal = 0;

        if (applySwitchDelay)
        {
            TargetSwitchLockTicksRemaining = Math.Max(TargetSwitchLockTicksRemaining, BattleTickMath.DurationToTicks(TargetSwitchDelay));
        }

        if (hadTarget)
        {
            RequestReevaluation(ReevaluationReason.TargetLost);
        }

        if (shouldRefundSignature)
        {
            RefundInterruptedSignatureCast();
        }
    }

    public void BeginWindup(BattleActionType actionType, EntityId? targetId, string? skillId)
    {
        var skill = ResolveSkill(skillId);
        var windupSeconds = actionType == BattleActionType.ActiveSkill && skill != null && skill.CastWindupSeconds > 0f
            ? skill.CastWindupSeconds
            : AttackWindup;
        PendingActionType = actionType;
        PendingTargetId = targetId;
        PendingSkillId = skillId;
        PendingLane = actionType == BattleActionType.BasicAttack ? ActionLane.Primary : skill?.Lane ?? ActionLane.Primary;
        PendingLockRule = actionType == BattleActionType.BasicAttack ? ActionLockRule.SoftCommit : skill?.LockRule ?? ActionLockRule.HardCommit;
        PendingSlotKind = actionType == BattleActionType.BasicAttack ? ActionSlotKind.BasicAttack : skill?.EffectiveSlotKind;
        var windupTicks = BattleTickMath.DurationToTicks(windupSeconds);
        ActionTicksRemaining = windupTicks;
        ActionTicksTotal = windupTicks;
        ActionState = CombatActionState.ExecuteAction;
        IsDefending = false;
        if (actionType == BattleActionType.ActiveSkill && skill?.UsesEnergy == true)
        {
            SpendSignatureCastEnergy();
        }

        StartRetargetLock(ResolveMinimumCommitSeconds(actionType, skill));
    }

    public void FinishWindup()
    {
        ActionTicksRemaining = 0;
        ActionTicksTotal = 0;
        PendingDecisionReason = DecisionReasonCode.DefaultCadence;
    }

    /// <summary>Records the action-choreography identity for the windup just begun. Called once per
    /// accepted BeginWindup; the id is read back when the contact resolves (Contacted intent) or the
    /// action is canceled (Canceled intent). Gameplay-inert.</summary>
    public void SetPendingActionInstance(ActionInstanceId id, int windupStartTick, int contactTick, SkillDelivery delivery)
    {
        PendingActionInstanceId = id;
        PendingWindupStartTick = windupStartTick;
        PendingContactTick = contactTick;
        PendingActionDelivery = delivery;
    }

    public void ClearPendingActionInstance()
    {
        PendingActionInstanceId = ActionInstanceId.None;
        PendingWindupStartTick = 0;
        PendingContactTick = 0;
        PendingActionDelivery = SkillDelivery.Melee;
    }

    public void StartRecovery(float? cooldownSeconds = null)
    {
        FinishWindup();
        CooldownTicksRemaining = BattleTickMath.DurationToTicks(cooldownSeconds ?? AttackCooldown);
        ActionState = CombatActionState.Recover;
        IsDefending = false;
        PendingLockRule = ActionLockRule.None;
        PendingSlotKind = null;
        PendingSkillId = null;
        PendingActionType = null;
        PendingTargetId = null;
        _pendingSignatureEnergySpent = false;
    }

    public void SetDefending()
    {
        PendingTargetId = null;
        PendingActionType = null;
        PendingSkillId = null;
        ActionTicksRemaining = 0;
        ActionTicksTotal = 0;
        IsDefending = true;
        ActionState = CombatActionState.Reposition;
    }

    public void StopDefending()
    {
        IsDefending = false;
        if (ActionState == CombatActionState.Reposition)
        {
            ActionState = CombatActionState.AcquireTarget;
        }
    }

    public void TakeDamage(float amount)
    {
        var dmg = Hp64.FromFloatQuantized(amount);
        if (_barrier.Raw > 0)
        {
            var absorbed = Hp64.Min(_barrier, dmg);
            _barrier -= absorbed;
            dmg -= absorbed;
        }

        _health = Hp64.Max(Hp64.Zero, _health - dmg);
        if (_health.Raw <= 0)
        {
            MarkDead();
            return;
        }

        RequestReevaluation(ReevaluationReason.TookHit);
    }

    public void Heal(float amount)
    {
        var adjusted = Hp64.FromFloatQuantized(amount) * Fixed32.FromFloatQuantized(GetHealingTakenMultiplier());
        _health = Hp64.Min(Hp64.FromFloatQuantized(MaxHealth), _health + adjusted);
    }

    public void AddBarrier(float amount)
    {
        _barrier = Hp64.Max(Hp64.Zero, _barrier + Hp64.FromFloatQuantized(amount));
    }

    public bool HasStatus(string statusId)
    {
        return _statuses.Any(status => string.Equals(status.StatusId, statusId, StringComparison.Ordinal));
    }

    /// <summary>전투당 1회성 트리거(OnHpBelow 등) latch. 처음 호출이면 true, 이후 동일 key 는 false.</summary>
    public bool TryLatchTrigger(string key)
    {
        return _firedTriggers.Add(key);
    }

    public float GetStatusMagnitude(string statusId)
    {
        return _statuses
            .Where(status => string.Equals(status.StatusId, statusId, StringComparison.Ordinal))
            .Select(status => status.Magnitude)
            .DefaultIfEmpty(0f)
            .Max();
    }

    public float GetIncomingDamageMultiplier()
    {
        var multiplier = 1f;
        if (HasStatus("marked"))
        {
            multiplier += Math.Max(0f, GetStatusMagnitude("marked"));
        }

        if (HasStatus("exposed"))
        {
            multiplier += Math.Max(0f, GetStatusMagnitude("exposed"));
        }

        if (IsGuarded)
        {
            // 콘텐츠 튜닝값(기본 -0.1) — status_family_guarded.asset IncomingDamageDelta.
            multiplier += _guardedIncomingDamageDelta;
        }

        return Math.Max(0.25f, multiplier);
    }

    public void ApplyStatus(
        StatusApplicationSpec spec,
        string sourceActorId = "",
        string sourceSkillId = "",
        string sourceApplicationId = "")
    {
        var existingIndex = _statuses.FindIndex(status => string.Equals(status.StatusId, spec.StatusId, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            var existing = _statuses[existingIndex];
            var durationTicks = BattleTickMath.DurationToTicks(spec.DurationSeconds);
            var stacks = Math.Min(spec.MaxStacks, existing.Stacks + 1);
            var remaining = spec.RefreshDurationOnReapply ? Math.Max(existing.RemainingTicks, durationTicks) : existing.RemainingTicks;
            var magnitude = Math.Max(existing.Magnitude, spec.Magnitude);
            var nextApplicationId = string.IsNullOrWhiteSpace(sourceApplicationId) ? spec.Id : sourceApplicationId;
            _statuses[existingIndex] = existing with
            {
                RemainingTicks = remaining,
                DurationTicks = Math.Max(existing.DurationTicks, durationTicks),
                Magnitude = magnitude,
                Stacks = stacks,
                SourceActorId = string.IsNullOrWhiteSpace(sourceActorId) ? existing.SourceActorId : sourceActorId,
                SourceSkillId = string.IsNullOrWhiteSpace(sourceSkillId) ? existing.SourceSkillId : sourceSkillId,
                SourceApplicationId = string.IsNullOrWhiteSpace(nextApplicationId) ? existing.SourceApplicationId : nextApplicationId,
            };
            return;
        }

        var newDurationTicks = BattleTickMath.DurationToTicks(spec.DurationSeconds);
        _statuses.Add(new AppliedStatusState(
            spec.StatusId,
            newDurationTicks,
            newDurationTicks,
            spec.Magnitude,
            1,
            sourceActorId,
            sourceSkillId,
            string.IsNullOrWhiteSpace(sourceApplicationId) ? spec.Id : sourceApplicationId));
    }

    public bool RemoveStatus(string statusId)
    {
        var removed = _statuses.RemoveAll(status => string.Equals(status.StatusId, statusId, StringComparison.Ordinal));
        return removed > 0;
    }

    public int RemoveStatuses(IEnumerable<string> statusIds)
    {
        var idSet = new HashSet<string>(statusIds, StringComparer.Ordinal);
        return _statuses.RemoveAll(status => idSet.Contains(status.StatusId));
    }

    public void ApplyControlResistWindow(float durationSeconds, float resistMultiplier)
    {
        if (durationSeconds <= 0f)
        {
            return;
        }

        var durationTicks = BattleTickMath.DurationToTicks(durationSeconds);
        ControlResistWindow = new ControlResistWindowState(
            Math.Max(durationTicks, ControlResistWindow?.RemainingTicks ?? 0),
            Math.Max(resistMultiplier, ControlResistWindow?.ResistMultiplier ?? 0f));
    }

    public BattleSkillSpec? ResolveSkill(string? skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return null;
        }

        if (Definition.EffectiveSignatureActive?.Id == skillId)
        {
            return Definition.EffectiveSignatureActive;
        }

        if (Definition.EffectiveFlexActive?.Id == skillId)
        {
            return Definition.EffectiveFlexActive;
        }

        return Definition.Skills.FirstOrDefault(skill => skill.Id == skillId);
    }

    public float ResolveActionRange(string? skillId)
    {
        var skill = ResolveSkill(skillId);
        return skill?.Range ?? AttackRange;
    }

    public float ResolveActionCooldown(string? skillId)
    {
        var skill = ResolveSkill(skillId);
        if (skill == null)
        {
            return AttackCooldown;
        }

        if (skill.UsesEnergy)
        {
            return 0f;
        }

        var cooldown = skill.BaseCooldownSeconds > 0f
            ? skill.BaseCooldownSeconds
            : AttackCooldown;
        return skill.EffectiveSlotKind == ActionSlotKind.FlexActive
            ? ApplySkillHaste(cooldown)
            : cooldown;
    }

    public bool CanUseMobility(float distanceToThreat, float? overrideMaxDistance = null)
    {
        var tolerance = Math.Max(0.1f, Behavior.RangeHysteresis * 1.5f);
        var triggerMaxDistance = overrideMaxDistance ?? Math.Max(Mobility?.TriggerMinDistance ?? 0f, Mobility?.TriggerMaxDistance ?? 0f);
        return Mobility is { IsEnabled: true } mobility
               && MobilityCooldownRemaining <= 0f
               && distanceToThreat >= Math.Max(0f, mobility.TriggerMinDistance - tolerance)
               && distanceToThreat <= triggerMaxDistance + tolerance;
    }

    public void StartMobilityCooldown()
    {
        if (Mobility is { } mobility)
        {
            MobilityCooldownTicksRemaining = BattleTickMath.DurationToTicks(ApplySkillHaste(Math.Max(0f, mobility.Cooldown)));
        }
    }

    public bool CanAttemptBlock => BlockCooldownRemaining <= 0f && Behavior.BlockChance > 0f && !IsStunned;

    public void TriggerBlockCooldown()
    {
        BlockCooldownTicksRemaining = BattleTickMath.DurationToTicks(Behavior.BlockCooldownSeconds);
    }

    public bool CanSpendSignatureCastEnergy()
    {
        return _energy >= Resource64.FromFloatQuantized(SignatureCastThreshold);
    }

    public void GainEnergyFromBasicAttackResolved()
    {
        GainEnergy(EnergyPerBasicAttack);
    }

    public void GainEnergyFromDirectHitTaken()
    {
        if (DirectHitEnergyIcdTicksRemaining > 0)
        {
            return;
        }

        GainEnergy(EnergyPerDirectHit);
        DirectHitEnergyIcdTicksRemaining = BattleTickMath.DurationToTicks(DirectHitEnergyIcdSeconds);
    }

    public void GainEnergyFromKill()
    {
        GainEnergy(EnergyPerKill);
    }

    public void GainEnergyFromAssist()
    {
        GainEnergy(EnergyPerAssist);
    }

    public void RequestReevaluation(ReevaluationReason reason)
    {
        if (reason == ReevaluationReason.None)
        {
            return;
        }

        if (PendingReevaluationReason == ReevaluationReason.None || reason != ReevaluationReason.Cadence)
        {
            PendingReevaluationReason = reason;
        }

        ReevaluationTicksRemaining = 0;
        MarkPositioningReplan(reason);
    }

    public bool SetPositioningIntent(PositioningIntentKind intent, ReevaluationReason replanReason)
    {
        var normalizedReason = replanReason == ReevaluationReason.None
            ? PositioningReplanReason
            : replanReason;
        if (PositioningIntent == intent && PositioningReplanReason == normalizedReason)
        {
            return false;
        }

        PositioningIntent = intent;
        PositioningReplanReason = normalizedReason;
        PositioningIntentRevision++;
        return true;
    }

    /// <summary>Phase 1: set the unit's current tactical-brain intent (chosen by RoleBrain). Returns true when
    /// <summary>다이브킬 귀속용 — 마지막 Dive 의도의 커밋 만료 step(transient, 재시뮬로 재현).
    /// 다이브 커밋(1.2s)이 킬보다 먼저 만료돼도 직후의 후열 킬은 그 다이브의 결과다.</summary>
    public int LastDiveCommitUntilStep { get; private set; } = int.MinValue;

    /// the intent type changed, so callers can emit an IntentChanged beat for presentation.</summary>
    public bool SetCombatIntent(CombatIntent intent)
    {
        var changed = CurrentCombatIntent.Type != intent.Type
                      || CurrentCombatIntent.TargetId != intent.TargetId;
        CurrentCombatIntent = intent;
        if (intent.Type == CombatIntentType.Dive)
        {
            LastDiveCommitUntilStep = Math.Max(LastDiveCommitUntilStep, intent.CommitUntilStep);
        }

        return changed;
    }

    /// <summary>Phase 1 anti-jitter: schedule the next step at which RoleBrain may re-decide this unit's intent.</summary>
    public void SetNextCombatIntentDecisionStep(int step)
    {
        NextCombatIntentDecisionStep = step;
    }

    private void MarkPositioningReplan(ReevaluationReason reason)
    {
        if (reason == ReevaluationReason.None)
        {
            return;
        }

        PositioningReplanReason = reason;
        PositioningIntentRevision++;
    }

    public void ConsumeReevaluation()
    {
        PendingReevaluationReason = ReevaluationReason.None;
        ReevaluationTicksRemaining = Math.Max(1, BattleTickMath.DurationToTicks(Behavior.ReevaluationInterval));
    }

    private void MarkDead()
    {
        _health = Hp64.Zero;
        _barrier = Hp64.Zero;
        _energy = Resource64.Clamp(_energy, Resource64.Zero, Resource64.FromFloatQuantized(MaxEnergy));
        IsDefending = false;
        ClearTarget(applySwitchDelay: false);
        CooldownTicksRemaining = 0;
        MobilityCooldownTicksRemaining = 0;
        BlockCooldownTicksRemaining = 0;
        DirectHitEnergyIcdTicksRemaining = 0;
        ActionState = CombatActionState.Dead;
        _statuses.Clear();
        ControlResistWindow = null;
    }

    public void Despawn()
    {
        MarkDead();
    }

    private float GetHealingTakenMultiplier()
    {
        var woundMagnitude = GetStatusMagnitude("wound");
        return Math.Max(0.1f, 1f - woundMagnitude);
    }

    private float GetSlowMultiplier()
    {
        var slowMagnitude = GetStatusMagnitude("slow");
        return Math.Max(0.1f, 1f - slowMagnitude);
    }

    // public: 기본공격/피격/처치 내부 경로 + 증강 트리거(GainEnergy op)의 범용 에너지 부여 공용 연산.
    public void GainEnergy(float amount)
    {
        if (amount <= 0f || MaxEnergy <= 0f)
        {
            return;
        }

        _energy = Resource64.Clamp(_energy + Resource64.FromFloatQuantized(amount), Resource64.Zero, Resource64.FromFloatQuantized(MaxEnergy));
    }

    private void SpendSignatureCastEnergy()
    {
        if (_pendingSignatureEnergySpent)
        {
            return;
        }

        if (!CanSpendSignatureCastEnergy())
        {
            return;
        }

        _energy = Resource64.Clamp(_energy - Resource64.FromFloatQuantized(SignatureCastThreshold), Resource64.Zero, Resource64.FromFloatQuantized(MaxEnergy));
        _pendingSignatureEnergySpent = true;
    }

    private void RefundInterruptedSignatureCast()
    {
        if (!_pendingSignatureEnergySpent)
        {
            return;
        }

        GainEnergy(InterruptedSignatureRefund);
        _pendingSignatureEnergySpent = false;
    }

    private float ResolveMinimumCommitSeconds(BattleActionType actionType, BattleSkillSpec? skill)
    {
        if (actionType == BattleActionType.BasicAttack)
        {
            return Definition.EffectiveBasicAttack.TargetRuleData?.MinimumCommitSeconds ?? 0.75f;
        }

        return skill?.TargetRuleData?.MinimumCommitSeconds ?? 0.75f;
    }

    private float ApplySkillHaste(float cooldownSeconds)
    {
        if (cooldownSeconds <= 0f)
        {
            return 0f;
        }

        return cooldownSeconds / Math.Max(1f, 1f + SkillHaste);
    }

    public List<AppliedStatusState> AdvanceStatusTimers()
    {
        var removed = new List<AppliedStatusState>();
        for (var index = _statuses.Count - 1; index >= 0; index--)
        {
            var status = _statuses[index];
            var remaining = status.RemainingTicks - 1;
            if (remaining <= 0)
            {
                removed.Add(status);
                _statuses.RemoveAt(index);
                continue;
            }

            _statuses[index] = status with { RemainingTicks = remaining };
        }

        return removed;
    }
}

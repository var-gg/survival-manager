using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Core.Numerics;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed partial class UnitSnapshot
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
    // WaitDefend 자세의 받는피해 delta 브리지 — 자세는 상태가 아니라 canonical guarded family의 delta를
    // 빌린다(현행 의미론 보존). guarded-kind 상태가 있으면 채널 엔트리가 가산하고 브리지는 침묵(중복 방지).
    private readonly float _guardedIncomingDamageDelta;
    // 채널 membership 엔트리 스냅샷(효과 종류 데이터화 3보 3f) — 규칙이 id ordinal 정렬로 파생한 배열
    // 참조를 생성 시 1회 확정(가산 순서 고정 = IEEE 결정성, 핫패스 조회 0). 합산 규칙: family 간 가산·
    // family 내 Max. ShredsDefense만 저장 magnitude×stack을 consumer에서 해석하고, 클램프는 코드 소유.
    private readonly StatusChannelEntry[] _amplifyIncomingEntries;
    private readonly StatusChannelEntry[] _guardedDefenseEntries;
    private readonly StatusChannelEntry[] _shredsDefenseEntries;
    private readonly StatusChannelEntry[] _reducesHealingEntries;
    private readonly StatusChannelEntry[] _dampensTempoEntries;
    // 규칙 미주입 레인(레거시 손조립 테스트)의 kind set 폴백 — 과거 sim 리터럴 HasStatus("...")와
    // 동일한 정적 set(결정성 보존, 1보 ?? -0.1f / 2보 ?? 1f와 같은 계약). 공유 인스턴스 — 변이 금지.
    private static readonly HashSet<string> FallbackUnstoppableStatusIds = new(StringComparer.Ordinal) { "unstoppable" };
    private static readonly HashSet<string> FallbackBlocksActiveSkillsStatusIds = new(StringComparer.Ordinal) { "silence" };
    private static readonly HashSet<string> FallbackBlocksMovementStatusIds = new(StringComparer.Ordinal) { "root" };
    private static readonly HashSet<string> FallbackBlocksActionStatusIds = new(StringComparer.Ordinal) { "stun" };
    private static readonly HashSet<string> FallbackMarksTargetStatusIds = new(StringComparer.Ordinal) { "marked" };
    // 채널 폴백(id ordinal 정렬) — 과거 sim 리터럴 배선과 항등(marked/exposed 가산·guarded -0.1·배율 1).
    private static readonly StatusChannelEntry[] FallbackAmplifyIncomingEntries = { new("exposed", 1f), new("marked", 1f) };
    private static readonly StatusChannelEntry[] FallbackGuardedDefenseEntries = { new("guarded", -0.1f) };
    private static readonly StatusChannelEntry[] FallbackShredsDefenseEntries = { new("sunder", 1f) };
    private static readonly StatusChannelEntry[] FallbackReducesHealingEntries = { new("wound", 1f) };
    private static readonly StatusChannelEntry[] FallbackDampensTempoEntries = { new("slow", 1f) };
    // kind별 상태 id set 스냅샷(효과 종류 데이터화 3보) — 생성 시 1회 확정, 핫패스 사전 조회 0.
    private readonly HashSet<string> _unstoppableStatusIds;
    private readonly HashSet<string> _blocksActiveSkillsStatusIds;
    private readonly HashSet<string> _blocksMovementStatusIds;
    private readonly HashSet<string> _blocksActionStatusIds;
    private readonly HashSet<string> _marksTargetStatusIds;
    private readonly Dictionary<string, int> _openingSkillCooldownTicks = new(StringComparer.Ordinal);
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
        // WaitDefend 브리지 delta — 규칙 미주입 레인(레거시 손조립 테스트)은 과거 sim 리터럴(-0.1)과
        // 동일한 기본값으로 떨어진다(결정성 보존).
        _guardedIncomingDamageDelta = statusRules?.ResolveIncomingDamageDelta("guarded") ?? -0.1f;
        // 채널 membership 엔트리(3f) — 규칙이 전투당 1회 파생한 ordinal 정렬 배열을 스냅샷. 미주입 레인은
        // 과거 리터럴 배선과 항등인 정적 폴백(2보 ?? 1f 계약의 채널 판).
        _amplifyIncomingEntries = statusRules?.AmplifyIncomingDamageEntries ?? FallbackAmplifyIncomingEntries;
        _guardedDefenseEntries = statusRules?.GuardedDefenseEntries ?? FallbackGuardedDefenseEntries;
        _shredsDefenseEntries = statusRules?.ShredsDefenseEntries ?? FallbackShredsDefenseEntries;
        _reducesHealingEntries = statusRules?.ReducesHealingEntries ?? FallbackReducesHealingEntries;
        _dampensTempoEntries = statusRules?.DampensTempoEntries ?? FallbackDampensTempoEntries;
        // 효과 종류 set(3보) — 규칙이 전투당 1회 파생한 set 참조를 스냅샷. 미주입 레인은 정적 폴백(결정성 보존).
        _unstoppableStatusIds = statusRules?.UnstoppableStatusIds ?? FallbackUnstoppableStatusIds;
        _blocksActiveSkillsStatusIds = statusRules?.BlocksActiveSkillsStatusIds ?? FallbackBlocksActiveSkillsStatusIds;
        _blocksMovementStatusIds = statusRules?.BlocksMovementStatusIds ?? FallbackBlocksMovementStatusIds;
        _blocksActionStatusIds = statusRules?.BlocksActionStatusIds ?? FallbackBlocksActionStatusIds;
        _marksTargetStatusIds = statusRules?.MarksTargetStatusIds ?? FallbackMarksTargetStatusIds;
        Anchor = definition.PreferredAnchor;
        FixedAnchorPosition = SpatialProjection.QuantizeToFixed(anchorPosition);
        FixedPosition = SpatialProjection.QuantizeToFixed(spawnPosition);
        ActionState = CombatActionState.Spawn;

        var modifiers = definition.Packages?.SelectMany(x => x.Modifiers).ToList() ?? new List<StatModifier>();
        Stats = new StatBlock(new Dictionary<StatKey, float>(definition.BaseStats), modifiers);
        InitializeSecondaryPressure(definition);
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
        InitializeOpeningSkillCooldown(definition.EffectiveSignatureActive);
        InitializeOpeningSkillCooldown(definition.EffectiveFlexActive);
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
    // 방어/저항 차감 채널(ShredsDefense membership, 3f) — Σ(magnitude×stack×배율) 가산,
    // 바닥 0 클램프 코드 소유. stack 소비는 이 채널에만 한정하며 저장 magnitude 병합은 Max를 유지한다.
    public float Armor => Math.Max(0f, Stats.Get(StatKey.Armor) - SumStackedChannelMagnitude(_shredsDefenseEntries));
    public float Resist => Math.Max(0f, Stats.Get(StatKey.Resist) - SumStackedChannelMagnitude(_shredsDefenseEntries));
    public float PhysPen => Math.Max(0f, Stats.Get(StatKey.PhysPen));
    public float MagPen => Math.Max(0f, Stats.Get(StatKey.MagPen));
    public float Lifesteal => Math.Max(0f, Stats.Get(StatKey.Lifesteal));
    public float Omnivamp => Math.Max(0f, Stats.Get(StatKey.Omnivamp));
    public float PhysPower => Stats.Get(StatKey.PhysPower);
    public float MagPower => Stats.Get(StatKey.MagPower);
    public float AttackSpeed => Math.Max(0.1f, Stats.Get(StatKey.AttackSpeed) * GetSlowMultiplier() * GetBloodrushMultiplier());
    public float HealPower => Stats.Get(StatKey.HealPower);
    public float MoveSpeed => Math.Max(0.1f, Stats.Get(StatKey.MoveSpeed) * GetSlowMultiplier() * GetBloodrushMultiplier());
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
    public bool HasBehaviorTag(string behaviorTag)
        => CombatBehaviorTags.Contains(Definition.RulePackages, behaviorTag);

    /// <summary>
    /// Authoritative HP-ratio comparison using the Q16.16 health snapshot. This avoids using the float
    /// read-model projection as a behavior discriminator.
    /// </summary>
    public bool IsHealthRatioAtOrBelow(int numerator, int denominator)
    {
        var maxHealth = Hp64.FromFloatQuantized(MaxHealth);
        if (denominator <= 0 || numerator < 0 || maxHealth.Raw <= 0)
        {
            return false;
        }

        return _health.Raw * denominator <= maxHealth.Raw * numerator;
    }

    public bool IsHealthRatioAtOrAbove(int numerator, int denominator)
    {
        var maxHealth = Hp64.FromFloatQuantized(MaxHealth);
        if (denominator <= 0 || numerator < 0 || maxHealth.Raw <= 0)
        {
            return false;
        }

        return _health.Raw * denominator >= maxHealth.Raw * numerator;
    }
    // 행동 차단 membership은 콘텐츠 kind(BlocksAction)가 파생한 set — "stun" 리터럴 조회의 승격(3보 3e).
    public bool IsStunned => HasAnyStatusOf(_blocksActionStatusIds);
    // 타게팅 표식 membership은 콘텐츠 kind(MarksTarget)가 파생한 set — "marked" 타게팅 조회의 승격(3보 3g).
    public bool IsMarkedTarget => HasAnyStatusOf(_marksTargetStatusIds);
    // 자발 이동 차단 membership은 콘텐츠 kind(BlocksMovement)가 파생한 set — "root" 리터럴 조회의 승격(3보 3d).
    public bool IsRooted => HasAnyStatusOf(_blocksMovementStatusIds);
    // 액티브 시전 차단 membership은 콘텐츠 kind(BlocksActiveSkills)가 파생한 set — "silence" 리터럴 조회의 승격(3보 3c).
    public bool IsSilenced => HasAnyStatusOf(_blocksActiveSkillsStatusIds);
    // 저지불가 membership은 콘텐츠 kind(GrantsUnstoppable)가 파생한 set — "unstoppable" 리터럴 조회의 승격(3보 3b).
    public bool IsUnstoppable => HasAnyStatusOf(_unstoppableStatusIds);
    // 수호 membership은 콘텐츠 kind(GrantsGuardedDefense)가 파생한 엔트리 — "guarded" 리터럴 조회의 승격(3f).
    public bool IsGuarded => HasAnyChannelStatus(_guardedDefenseEntries) || IsDefending;
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

        foreach (var skillId in _openingSkillCooldownTicks.Keys.ToArray())
        {
            var remaining = _openingSkillCooldownTicks[skillId];
            if (remaining <= 0)
            {
                continue;
            }

            remaining--;
            _openingSkillCooldownTicks[skillId] = remaining;
            if (remaining == 0)
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
        var interruptedSkill = PendingActionType == BattleActionType.ActiveSkill
            ? ResolveSkill(PendingSkillId)
            : null;
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

        if (interruptedSkill?.DisplacementKind == SkillDisplacementKind.SelfBlinkToTarget)
        {
            var interruptedCooldown = interruptedSkill.BaseCooldownSeconds * (1f - interruptedSkill.InterruptRefundScalar);
            CooldownTicksRemaining = Math.Max(
                CooldownTicksRemaining,
                BattleTickMath.DurationToTicks(Math.Max(0f, interruptedCooldown)));
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
        _ = HealMeasured(amount);
    }

    internal HealingApplicationResult HealMeasured(float amount)
    {
        var adjusted = Hp64.FromFloatQuantized(amount) * Fixed32.FromFloatQuantized(GetHealingTakenMultiplier());
        var before = _health;
        _health = Hp64.Min(Hp64.FromFloatQuantized(MaxHealth), _health + adjusted);
        return new HealingApplicationResult(
            amount,
            adjusted.ToFloat(),
            (_health - before).ToFloat());
    }

    public void AddBarrier(float amount)
    {
        _barrier = Hp64.Max(Hp64.Zero, _barrier + Hp64.FromFloatQuantized(amount));
    }

    public bool HasStatus(string statusId)
    {
        return _statuses.Any(status => string.Equals(status.StatusId, statusId, StringComparison.Ordinal));
    }

    /// <summary>보유 상태 중 하나라도 주어진 효과 종류 set에 속하는가 — kind 데이터화(3보)의 핫패스 소비.
    /// set은 CombatStatusRules가 전투당 1회 파생한 불변 스냅샷, 여기선 Contains만(클로저 없는 순회).</summary>
    private bool HasAnyStatusOf(HashSet<string> statusIds)
    {
        foreach (var status in _statuses)
        {
            if (statusIds.Contains(status.StatusId))
            {
                return true;
            }
        }

        return false;
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
        // 받는 피해 채널(3f) — 증폭 membership(magnitude×배율, 음수 차단)과 수호 membership(family delta)이
        // family 간 가산. 엔트리는 id ordinal 정렬(가산 순서 고정), 바닥 0.25 클램프는 코드 소유.
        var multiplier = 1f;
        foreach (var entry in _amplifyIncomingEntries)
        {
            if (HasStatus(entry.StatusId))
            {
                multiplier += Math.Max(0f, GetStatusMagnitude(entry.StatusId) * entry.Value);
            }
        }

        var guardedByStatus = false;
        foreach (var entry in _guardedDefenseEntries)
        {
            if (HasStatus(entry.StatusId))
            {
                multiplier += entry.Value;
                guardedByStatus = true;
            }
        }

        // WaitDefend 자세는 상태가 아니므로 canonical guarded delta 브리지로 가산 — guarded-kind 상태가
        // 이미 가산됐으면 침묵(현행 "상태 or 자세 = delta 1회" 의미론 보존).
        if (!guardedByStatus && IsDefending)
        {
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
            var stacks = Math.Max(existing.Stacks, Math.Min(spec.MaxStacks, existing.Stacks + 1));
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

    public bool IsSkillOpeningLocked(BattleSkillSpec skill)
    {
        return skill.StartsOnCooldown
               && _openingSkillCooldownTicks.TryGetValue(skill.Id, out var remaining)
               && remaining > 0;
    }

    public float ResolveOpeningSkillCooldownRemaining(string skillId)
    {
        return _openingSkillCooldownTicks.TryGetValue(skillId, out var remaining)
            ? remaining * BattleTickMath.TickSeconds
            : 0f;
    }

    private void InitializeOpeningSkillCooldown(BattleSkillSpec? skill)
    {
        if (skill is not { StartsOnCooldown: true } || skill.OpeningLockSeconds <= 0f)
        {
            return;
        }

        var ticks = BattleTickMath.DurationToTicks(skill.OpeningLockSeconds);
        _openingSkillCooldownTicks[skill.Id] = Math.Max(
            _openingSkillCooldownTicks.GetValueOrDefault(skill.Id),
            ticks);
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

    /// <summary>
    /// 점멸 착지 위치를 Dive 시도 기하의 새 기준점으로 한 번 갱신한다. 커밋 만료는 그대로 둬
    /// 착지가 지속 시간 연장이 되지 않게 한다.
    /// </summary>
    public bool ReanchorDiveEntry(int stepIndex, EntityId targetId)
    {
        if (CurrentCombatIntent.Type != CombatIntentType.Dive
            || CurrentCombatIntent.TargetId != targetId)
        {
            return false;
        }

        CurrentCombatIntent = CurrentCombatIntent with
        {
            DiveEntryStep = stepIndex,
            DiveEntryTargetId = targetId,
            DiveEntryPathDistance = 0f,
            DiveEntryActorPosition = Position,
        };
        return true;
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
        _openingSkillCooldownTicks.Clear();
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
        // 치유 감소 채널(ReducesHealing membership, 3f) — family 간 가산, 바닥 0.1 클램프 코드 소유.
        return Math.Max(0.1f, 1f - SumChannelMagnitude(_reducesHealingEntries));
    }

    private float GetSlowMultiplier()
    {
        // 공속/이속 감쇠 채널(DampensTempo membership, 3f) — family 간 가산, 바닥 0.1 클램프 코드 소유.
        return Math.Max(0.1f, 1f - SumChannelMagnitude(_dampensTempoEntries));
    }

    /// <summary>채널 membership 엔트리의 magnitude×배율 합(3f 합산 규칙: family 간 가산, family 내는
    /// 스택 병합 Max). 엔트리는 규칙이 ordinal 정렬로 파생한 불변 배열 — 가산 순서 고정(IEEE 결정성).</summary>
    private float SumChannelMagnitude(StatusChannelEntry[] entries)
    {
        var total = 0f;
        foreach (var entry in entries)
        {
            total += GetStatusMagnitude(entry.StatusId) * entry.Value;
        }

        return total;
    }

    /// <summary>ShredsDefense 전용 magnitude×stack×배율 합. global status merge는 Max로 유지하고
    /// 실제 stack 축적 효과만 flat 방어/저항 차감 consumer에서 해석한다.</summary>
    private float SumStackedChannelMagnitude(StatusChannelEntry[] entries)
    {
        var total = 0f;
        foreach (var entry in entries)
        {
            var stackedMagnitude = _statuses
                .Where(status => string.Equals(status.StatusId, entry.StatusId, StringComparison.Ordinal))
                .Select(status => status.Magnitude * status.Stacks)
                .DefaultIfEmpty(0f)
                .Max();
            total += stackedMagnitude * entry.Value;
        }

        return total;
    }

    /// <summary>보유 상태 중 하나라도 채널 membership 엔트리에 속하는가 — IsGuarded 등 membership 게터 소비.</summary>
    private bool HasAnyChannelStatus(StatusChannelEntry[] entries)
    {
        foreach (var entry in entries)
        {
            if (HasStatus(entry.StatusId))
            {
                return true;
            }
        }

        return false;
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
            if (status.RemainingTicks == int.MaxValue)
            {
                continue;
            }

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

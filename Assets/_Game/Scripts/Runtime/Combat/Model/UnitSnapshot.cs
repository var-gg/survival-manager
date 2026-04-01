using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Ids;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed class UnitSnapshot
{
    private readonly List<AppliedStatusState> _statuses = new();

    public UnitSnapshot(
        EntityId id,
        TeamSide side,
        BattleUnitLoadout definition,
        CombatVector2 anchorPosition,
        CombatVector2 spawnPosition)
    {
        Id = id;
        Side = side;
        Definition = definition;
        Anchor = definition.PreferredAnchor;
        AnchorPosition = anchorPosition;
        Position = spawnPosition;
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
        CurrentHealth = MaxHealth;
        RequestReevaluation(ReevaluationReason.Cadence);
    }

    public EntityId Id { get; }
    public TeamSide Side { get; }
    public BattleUnitLoadout Definition { get; }
    public DeploymentAnchorId Anchor { get; }
    public CombatVector2 AnchorPosition { get; }
    public CombatVector2 Position { get; private set; }
    public StatBlock Stats { get; }
    public FootprintProfile Footprint { get; }
    public BehaviorProfile Behavior { get; }
    public MobilityActionProfile? Mobility { get; }
    public CombatActionState ActionState { get; private set; }
    public EntityId? CurrentTargetId { get; private set; }
    public EntityId? PendingTargetId { get; private set; }
    public BattleActionType? PendingActionType { get; private set; }
    public string? PendingSkillId { get; private set; }
    public float ActionTimerRemaining { get; private set; }
    public float ActionTimerTotal { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float TargetSwitchLockRemaining { get; private set; }
    public float ReevaluationRemaining { get; private set; }
    public ReevaluationReason PendingReevaluationReason { get; private set; }
    public float MobilityCooldownRemaining { get; private set; }
    public float BlockCooldownRemaining { get; private set; }
    public float CurrentHealth { get; private set; }
    public float Barrier { get; private set; }
    public IReadOnlyList<AppliedStatusState> Statuses => _statuses;
    public ControlResistWindowState? ControlResistWindow { get; private set; }
    public EngagementSlotAssignment? EngagementSlot { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool IsDefending { get; private set; }
    public bool NeedsReevaluation => PendingReevaluationReason != ReevaluationReason.None || ReevaluationRemaining <= 0f;
    public float MaxHealth => Stats.Get(StatKey.MaxHealth);
    public float Armor => Math.Max(0f, Stats.Get(StatKey.Armor) - GetStatusMagnitude("sunder"));
    public float Resist => Math.Max(0f, Stats.Get(StatKey.Resist) - GetStatusMagnitude("sunder"));
    public float PhysPower => Stats.Get(StatKey.PhysPower);
    public float MagPower => Stats.Get(StatKey.MagPower);
    public float AttackSpeed => Math.Max(0.1f, Stats.Get(StatKey.AttackSpeed) * GetSlowMultiplier());
    public float HealPower => Stats.Get(StatKey.HealPower);
    public float MoveSpeed => Math.Max(0.1f, Stats.Get(StatKey.MoveSpeed) * GetSlowMultiplier());
    public float AttackRange => Math.Max(0.5f, Stats.Get(StatKey.AttackRange));
    public float ManaMax => Stats.Get(StatKey.ManaMax);
    public float ManaGainOnAttack => Stats.Get(StatKey.ManaGainOnAttack);
    public float ManaGainOnHit => Stats.Get(StatKey.ManaGainOnHit);
    public float CooldownRecovery => Stats.Get(StatKey.CooldownRecovery);
    public float AggroRadius => Math.Max(AttackRange, Stats.Get(StatKey.AggroRadius));
    public FloatRange PreferredRangeBand => Footprint.PreferredRangeBand;
    public float PreferredDistance => Definition.PreferredDistance > 0f
        ? Definition.PreferredDistance
        : Footprint.PreferredRangeBand.Midpoint;
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
    public float AttackCooldown => Math.Max(0.1f, Stats.Get(StatKey.AttackCooldown));
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
    public float WindupProgress => ActionState == CombatActionState.ExecuteAction && ActionTimerTotal > 0f
        ? 1f - (ActionTimerRemaining / ActionTimerTotal)
        : 0f;

    public void AdvanceTime(float deltaSeconds)
    {
        if (ActionTimerRemaining > 0f)
        {
            ActionTimerRemaining = Math.Max(0f, ActionTimerRemaining - deltaSeconds);
        }

        var previousCooldown = CooldownRemaining;
        if (CooldownRemaining > 0f)
        {
            CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaSeconds);
            if (previousCooldown > 0f && CooldownRemaining <= 0f)
            {
                RequestReevaluation(ReevaluationReason.SkillReady);
            }
        }

        var previousMobilityCooldown = MobilityCooldownRemaining;
        if (MobilityCooldownRemaining > 0f)
        {
            MobilityCooldownRemaining = Math.Max(0f, MobilityCooldownRemaining - deltaSeconds);
            if (previousMobilityCooldown > 0f && MobilityCooldownRemaining <= 0f && Mobility is { IsEnabled: true })
            {
                RequestReevaluation(ReevaluationReason.MobilityReady);
            }
        }

        if (BlockCooldownRemaining > 0f)
        {
            BlockCooldownRemaining = Math.Max(0f, BlockCooldownRemaining - deltaSeconds);
        }

        if (TargetSwitchLockRemaining > 0f)
        {
            TargetSwitchLockRemaining = Math.Max(0f, TargetSwitchLockRemaining - deltaSeconds);
        }

        if (ReevaluationRemaining > 0f)
        {
            ReevaluationRemaining = Math.Max(0f, ReevaluationRemaining - deltaSeconds);
        }

        if (ControlResistWindow is { } controlResist)
        {
            var remaining = Math.Max(0f, controlResist.RemainingSeconds - deltaSeconds);
            ControlResistWindow = remaining > 0f
                ? controlResist with { RemainingSeconds = remaining }
                : null;
        }
    }

    public void SetPosition(CombatVector2 position)
    {
        Position = position;
    }

    public void SetActionState(CombatActionState actionState)
    {
        ActionState = actionState;
    }

    public void SetCurrentTarget(EntityId? targetId)
    {
        CurrentTargetId = targetId;
    }

    public void ClearTarget(bool applySwitchDelay)
    {
        var hadTarget = CurrentTargetId != null || PendingTargetId != null;
        CurrentTargetId = null;
        PendingTargetId = null;
        PendingActionType = null;
        PendingSkillId = null;
        ActionTimerRemaining = 0f;
        ActionTimerTotal = 0f;
        EngagementSlot = null;

        if (applySwitchDelay)
        {
            TargetSwitchLockRemaining = Math.Max(TargetSwitchLockRemaining, TargetSwitchDelay);
        }

        if (hadTarget)
        {
            RequestReevaluation(ReevaluationReason.TargetLost);
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
        ActionTimerRemaining = windupSeconds;
        ActionTimerTotal = windupSeconds;
        ActionState = CombatActionState.ExecuteAction;
        IsDefending = false;
    }

    public void FinishWindup()
    {
        ActionTimerRemaining = 0f;
        ActionTimerTotal = 0f;
    }

    public void StartRecovery(float? cooldownSeconds = null)
    {
        FinishWindup();
        CooldownRemaining = cooldownSeconds ?? AttackCooldown;
        ActionState = CombatActionState.Recover;
        IsDefending = false;
    }

    public void SetDefending()
    {
        PendingTargetId = null;
        PendingActionType = null;
        PendingSkillId = null;
        ActionTimerRemaining = 0f;
        ActionTimerTotal = 0f;
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
        if (Barrier > 0f)
        {
            var absorbed = Math.Min(Barrier, amount);
            Barrier -= absorbed;
            amount -= absorbed;
        }

        CurrentHealth = Math.Max(0f, CurrentHealth - amount);
        if (CurrentHealth <= 0f)
        {
            MarkDead();
            return;
        }

        RequestReevaluation(ReevaluationReason.TookHit);
    }

    public void Heal(float amount)
    {
        var adjusted = amount * GetHealingTakenMultiplier();
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + adjusted);
    }

    public void AddBarrier(float amount)
    {
        Barrier = Math.Max(0f, Barrier + amount);
    }

    public bool HasStatus(string statusId)
    {
        return _statuses.Any(status => string.Equals(status.StatusId, statusId, StringComparison.Ordinal));
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
            multiplier -= 0.1f;
        }

        return Math.Max(0.25f, multiplier);
    }

    public void ApplyStatus(StatusApplicationSpec spec)
    {
        var existingIndex = _statuses.FindIndex(status => string.Equals(status.StatusId, spec.StatusId, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            var existing = _statuses[existingIndex];
            var stacks = Math.Min(spec.MaxStacks, existing.Stacks + 1);
            var remaining = spec.RefreshDurationOnReapply ? Math.Max(existing.RemainingSeconds, spec.DurationSeconds) : existing.RemainingSeconds;
            var magnitude = Math.Max(existing.Magnitude, spec.Magnitude);
            _statuses[existingIndex] = existing with
            {
                RemainingSeconds = remaining,
                DurationSeconds = Math.Max(existing.DurationSeconds, spec.DurationSeconds),
                Magnitude = magnitude,
                Stacks = stacks,
            };
            return;
        }

        _statuses.Add(new AppliedStatusState(spec.StatusId, spec.DurationSeconds, spec.DurationSeconds, spec.Magnitude));
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

        ControlResistWindow = new ControlResistWindowState(
            Math.Max(durationSeconds, ControlResistWindow?.RemainingSeconds ?? 0f),
            Math.Max(resistMultiplier, ControlResistWindow?.ResistMultiplier ?? 0f));
    }

    public BattleSkillSpec? ResolveSkill(string? skillId)
    {
        return string.IsNullOrWhiteSpace(skillId)
            ? null
            : Definition.Skills.FirstOrDefault(skill => skill.Id == skillId);
    }

    public float ResolveActionRange(string? skillId)
    {
        var skill = ResolveSkill(skillId);
        return skill?.Range ?? AttackRange;
    }

    public float ResolveActionCooldown(string? skillId)
    {
        var skill = ResolveSkill(skillId);
        return skill != null && skill.BaseCooldownSeconds > 0f
            ? skill.BaseCooldownSeconds
            : AttackCooldown;
    }

    public void SetEngagementSlot(EngagementSlotAssignment? slot)
    {
        var changed = EngagementSlot?.TargetId != slot?.TargetId
                      || EngagementSlot?.SlotIndex != slot?.SlotIndex
                      || EngagementSlot?.IsOverflow != slot?.IsOverflow;
        EngagementSlot = slot;
        if (changed)
        {
            RequestReevaluation(slot == null ? ReevaluationReason.SlotLost : ReevaluationReason.Cadence);
        }
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
            MobilityCooldownRemaining = Math.Max(0f, mobility.Cooldown);
        }
    }

    public bool CanAttemptBlock => BlockCooldownRemaining <= 0f && Behavior.BlockChance > 0f && !IsStunned;

    public void TriggerBlockCooldown()
    {
        BlockCooldownRemaining = Math.Max(0f, Behavior.BlockCooldownSeconds);
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

        ReevaluationRemaining = 0f;
    }

    public void ConsumeReevaluation()
    {
        PendingReevaluationReason = ReevaluationReason.None;
        ReevaluationRemaining = Math.Max(0.1f, Behavior.ReevaluationInterval);
    }

    private void MarkDead()
    {
        CurrentHealth = 0f;
        Barrier = 0f;
        IsDefending = false;
        ClearTarget(applySwitchDelay: false);
        CooldownRemaining = 0f;
        MobilityCooldownRemaining = 0f;
        BlockCooldownRemaining = 0f;
        ActionState = CombatActionState.Dead;
        _statuses.Clear();
        ControlResistWindow = null;
        EngagementSlot = null;
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

    public List<string> AdvanceStatusTimers(float deltaSeconds)
    {
        var removed = new List<string>();
        for (var index = _statuses.Count - 1; index >= 0; index--)
        {
            var status = _statuses[index];
            var remaining = Math.Max(0f, status.RemainingSeconds - deltaSeconds);
            if (remaining <= 0f)
            {
                removed.Add(status.StatusId);
                _statuses.RemoveAt(index);
                continue;
            }

            _statuses[index] = status with { RemainingSeconds = remaining };
        }

        return removed;
    }
}

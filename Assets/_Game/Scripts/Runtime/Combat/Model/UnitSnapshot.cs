using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Ids;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed class UnitSnapshot
{
    public UnitSnapshot(
        EntityId id,
        TeamSide side,
        UnitDefinition definition,
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
        CurrentHealth = MaxHealth;
    }

    public EntityId Id { get; }
    public TeamSide Side { get; }
    public UnitDefinition Definition { get; }
    public DeploymentAnchorId Anchor { get; }
    public CombatVector2 AnchorPosition { get; }
    public CombatVector2 Position { get; private set; }
    public StatBlock Stats { get; }
    public CombatActionState ActionState { get; private set; }
    public EntityId? CurrentTargetId { get; private set; }
    public EntityId? PendingTargetId { get; private set; }
    public BattleActionType? PendingActionType { get; private set; }
    public string? PendingSkillId { get; private set; }
    public float ActionTimerRemaining { get; private set; }
    public float ActionTimerTotal { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float TargetSwitchLockRemaining { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool IsDefending { get; private set; }
    public float MaxHealth => Stats.Get(StatKey.MaxHealth);
    public float Attack => Stats.Get(StatKey.Attack);
    public float Defense => Stats.Get(StatKey.Defense);
    public float Speed => Stats.Get(StatKey.Speed);
    public float HealPower => Stats.Get(StatKey.HealPower);
    public float MoveSpeed => Stats.Get(StatKey.MoveSpeed);
    public float AttackRange => Math.Max(0.5f, Stats.Get(StatKey.AttackRange));
    public float AggroRadius => Math.Max(AttackRange, Stats.Get(StatKey.AggroRadius));
    public float AttackWindup => Math.Max(0.05f, Stats.Get(StatKey.AttackWindup));
    public float AttackCooldown => Math.Max(0.1f, Stats.Get(StatKey.AttackCooldown));
    public float LeashDistance => Math.Max(0.5f, Stats.Get(StatKey.LeashDistance));
    public float TargetSwitchDelay => Math.Max(0f, Stats.Get(StatKey.TargetSwitchDelay));
    public float HealthRatio => MaxHealth <= 0 ? 0 : CurrentHealth / MaxHealth;
    public float WindupProgress => ActionState == CombatActionState.Windup && ActionTimerTotal > 0f
        ? 1f - (ActionTimerRemaining / ActionTimerTotal)
        : 0f;

    public void AdvanceTime(float deltaSeconds)
    {
        if (ActionTimerRemaining > 0f)
        {
            ActionTimerRemaining = Math.Max(0f, ActionTimerRemaining - deltaSeconds);
        }

        if (CooldownRemaining > 0f)
        {
            CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaSeconds);
        }

        if (TargetSwitchLockRemaining > 0f)
        {
            TargetSwitchLockRemaining = Math.Max(0f, TargetSwitchLockRemaining - deltaSeconds);
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
        CurrentTargetId = null;
        PendingTargetId = null;
        PendingActionType = null;
        PendingSkillId = null;
        ActionTimerRemaining = 0f;
        ActionTimerTotal = 0f;

        if (applySwitchDelay)
        {
            TargetSwitchLockRemaining = Math.Max(TargetSwitchLockRemaining, TargetSwitchDelay);
        }
    }

    public void BeginWindup(BattleActionType actionType, EntityId? targetId, string? skillId)
    {
        PendingActionType = actionType;
        PendingTargetId = targetId;
        PendingSkillId = skillId;
        ActionTimerRemaining = AttackWindup;
        ActionTimerTotal = AttackWindup;
        ActionState = CombatActionState.Windup;
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
        ActionState = CombatActionState.Recovery;
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
            ActionState = CombatActionState.SeekTarget;
        }
    }

    public void TakeDamage(float amount)
    {
        CurrentHealth = Math.Max(0f, CurrentHealth - amount);
        if (CurrentHealth <= 0f)
        {
            MarkDead();
        }
    }

    public void Heal(float amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
    }

    public SkillDefinition? ResolveSkill(string? skillId)
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

    private void MarkDead()
    {
        CurrentHealth = 0f;
        IsDefending = false;
        ClearTarget(applySwitchDelay: false);
        CooldownRemaining = 0f;
        ActionState = CombatActionState.Dead;
    }
}

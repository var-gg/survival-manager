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
        CurrentHealth = MaxHealth;
    }

    public EntityId Id { get; }
    public TeamSide Side { get; }
    public BattleUnitLoadout Definition { get; }
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
    public float Armor => Stats.Get(StatKey.Armor);
    public float Resist => Stats.Get(StatKey.Resist);
    public float PhysPower => Stats.Get(StatKey.PhysPower);
    public float MagPower => Stats.Get(StatKey.MagPower);
    public float AttackSpeed => Math.Max(0.1f, Stats.Get(StatKey.AttackSpeed));
    public float HealPower => Stats.Get(StatKey.HealPower);
    public float MoveSpeed => Stats.Get(StatKey.MoveSpeed);
    public float AttackRange => Math.Max(0.5f, Stats.Get(StatKey.AttackRange));
    public float ManaMax => Stats.Get(StatKey.ManaMax);
    public float ManaGainOnAttack => Stats.Get(StatKey.ManaGainOnAttack);
    public float ManaGainOnHit => Stats.Get(StatKey.ManaGainOnHit);
    public float CooldownRecovery => Stats.Get(StatKey.CooldownRecovery);
    public float AggroRadius => Math.Max(AttackRange, Stats.Get(StatKey.AggroRadius));
    public float PreferredDistance => Math.Max(0f, Definition.PreferredDistance > 0f ? Definition.PreferredDistance : Stats.Get(StatKey.PreferredDistance));
    public float ProtectRadius => Math.Max(0f, Definition.ProtectRadius > 0f ? Definition.ProtectRadius : Stats.Get(StatKey.ProtectRadius));
    public float AttackWindup => Math.Max(0.05f, Stats.Get(StatKey.AttackWindup));
    public float CastWindup => Math.Max(0.05f, Stats.Get(StatKey.CastWindup));
    public float ProjectileSpeed => Math.Max(0f, Stats.Get(StatKey.ProjectileSpeed));
    public float CollisionRadius => Math.Max(0.1f, Stats.Get(StatKey.CollisionRadius));
    public float RepositionCooldown => Math.Max(0f, Stats.Get(StatKey.RepositionCooldown));
    public float AttackCooldown => Math.Max(0.1f, Stats.Get(StatKey.AttackCooldown));
    public float LeashDistance => Math.Max(0.5f, Stats.Get(StatKey.LeashDistance));
    public float TargetSwitchDelay => Math.Max(0f, Stats.Get(StatKey.TargetSwitchDelay));
    public float Attack => PhysPower;
    public float Defense => Armor;
    public float Speed => AttackSpeed;
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
        var skill = ResolveSkill(skillId);
        var windupSeconds = actionType == BattleActionType.ActiveSkill && skill != null && skill.CastWindupSeconds > 0f
            ? skill.CastWindupSeconds
            : AttackWindup;
        PendingActionType = actionType;
        PendingTargetId = targetId;
        PendingSkillId = skillId;
        ActionTimerRemaining = windupSeconds;
        ActionTimerTotal = windupSeconds;
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

    private void MarkDead()
    {
        CurrentHealth = 0f;
        IsDefending = false;
        ClearTarget(applySwitchDelay: false);
        CooldownRemaining = 0f;
        ActionState = CombatActionState.Dead;
    }
}

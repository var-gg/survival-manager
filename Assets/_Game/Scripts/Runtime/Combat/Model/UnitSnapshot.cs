using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Ids;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed class UnitSnapshot
{
    public UnitSnapshot(EntityId id, TeamSide side, UnitDefinition definition)
    {
        Id = id;
        Side = side;
        Definition = definition;

        var modifiers = definition.Packages?.SelectMany(x => x.Modifiers).ToList() ?? new List<StatModifier>();
        Stats = new StatBlock(new Dictionary<StatKey, float>(definition.BaseStats), modifiers);
        CurrentHealth = MaxHealth;
    }

    public EntityId Id { get; }
    public TeamSide Side { get; }
    public UnitDefinition Definition { get; }
    public StatBlock Stats { get; }
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool IsDefending { get; private set; }
    public float MaxHealth => Stats.Get(StatKey.MaxHealth);
    public float Attack => Stats.Get(StatKey.Attack);
    public float Defense => Stats.Get(StatKey.Defense);
    public float Speed => Stats.Get(StatKey.Speed);
    public float HealPower => Stats.Get(StatKey.HealPower);
    public float HealthRatio => MaxHealth <= 0 ? 0 : CurrentHealth / MaxHealth;

    public void ResetTurnState()
    {
        IsDefending = false;
    }

    public void SetDefending()
    {
        IsDefending = true;
    }

    public void TakeDamage(float amount)
    {
        CurrentHealth = Math.Max(0f, CurrentHealth - amount);
    }

    public void Heal(float amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
    }
}

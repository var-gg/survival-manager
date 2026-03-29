using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Unity;

public static class BattleReplayBuilder
{
    private const float IntroDurationSeconds = 0.55f;
    private const float ResultDurationSeconds = 0.60f;

    public static BattleReplayTrack Build(BattleState initialState, BattleResult result)
    {
        var order = initialState.Allies.Concat(initialState.Enemies).Select(unit => unit.Id.Value).ToList();
        var actors = order.ToDictionary(id => id, id => BuildMutableState(initialState, id));
        var initialRoster = SnapshotActors(order, actors);
        var frames = new List<BattleReplayFrame>
        {
            new(
                BattleReplayFrameKind.Intro,
                0,
                0,
                null,
                null,
                null,
                null,
                null,
                0f,
                "battle_intro",
                null,
                null,
                null,
                null,
                IntroDurationSeconds,
                initialRoster)
        };

        var sequenceIndex = 1;
        foreach (var battleEvent in result.Events)
        {
            frames.Add(ApplyEvent(sequenceIndex++, battleEvent, order, actors));
        }

        frames.Add(new BattleReplayFrame(
            BattleReplayFrameKind.Result,
            sequenceIndex,
            result.TickCount,
            null,
            null,
            null,
            null,
            null,
            0f,
            result.Winner == TeamSide.Ally ? "battle_result_victory" : "battle_result_defeat",
            null,
            null,
            null,
            null,
            ResultDurationSeconds,
            SnapshotActors(order, actors)));

        return new BattleReplayTrack(initialRoster, frames, result.Winner, result.TickCount, result.Events.Count);
    }

    private static BattleReplayFrame ApplyEvent(
        int sequenceIndex,
        BattleEvent battleEvent,
        IReadOnlyList<string> order,
        IReadOnlyDictionary<string, MutableActorState> actors)
    {
        var source = actors[battleEvent.ActorId.Value];
        source.IsDefending = false;

        MutableActorState? target = null;
        if (battleEvent.TargetId is { } targetId && actors.TryGetValue(targetId.Value, out var resolvedTarget))
        {
            target = resolvedTarget;
        }

        var beforeSourceHealth = source.CurrentHealth;
        var beforeTargetHealth = target?.CurrentHealth;

        switch (battleEvent.ActionType)
        {
            case BattleActionType.BasicAttack:
                target?.TakeDamage(battleEvent.Value);
                break;
            case BattleActionType.ActiveSkill:
                if (battleEvent.Note == "heal_skill")
                {
                    target?.Heal(battleEvent.Value);
                }
                else
                {
                    target?.TakeDamage(battleEvent.Value);
                }
                break;
            case BattleActionType.WaitDefend:
                source.IsDefending = true;
                break;
        }

        return new BattleReplayFrame(
            BattleReplayFrameKind.Event,
            sequenceIndex,
            battleEvent.Tick,
            battleEvent.ActionType,
            source.Id,
            source.Name,
            target?.Id,
            target?.Name,
            battleEvent.Value,
            battleEvent.Note,
            beforeSourceHealth,
            source.CurrentHealth,
            beforeTargetHealth,
            target?.CurrentHealth,
            ResolveEventDuration(battleEvent),
            SnapshotActors(order, actors));
    }

    private static float ResolveEventDuration(BattleEvent battleEvent)
    {
        return battleEvent.ActionType switch
        {
            BattleActionType.BasicAttack => 0.52f,
            BattleActionType.ActiveSkill when battleEvent.Note == "heal_skill" => 0.60f,
            BattleActionType.ActiveSkill => 0.56f,
            BattleActionType.WaitDefend => 0.40f,
            _ => 0.45f
        };
    }

    private static MutableActorState BuildMutableState(BattleState initialState, string actorId)
    {
        var unit = initialState.AllUnits.First(snapshot => snapshot.Id.Value == actorId);
        return new MutableActorState(
            unit.Id.Value,
            unit.Definition.Name,
            unit.Side,
            unit.Definition.Row,
            unit.Definition.RaceId,
            unit.Definition.ClassId,
            unit.CurrentHealth,
            unit.MaxHealth);
    }

    private static IReadOnlyList<BattleReplayActorSnapshot> SnapshotActors(
        IReadOnlyList<string> order,
        IReadOnlyDictionary<string, MutableActorState> actors)
    {
        return order.Select(id => actors[id].ToSnapshot()).ToList();
    }

    private sealed class MutableActorState
    {
        public MutableActorState(
            string id,
            string name,
            TeamSide side,
            RowPosition row,
            string raceId,
            string classId,
            float currentHealth,
            float maxHealth)
        {
            Id = id;
            Name = name;
            Side = side;
            Row = row;
            RaceId = raceId;
            ClassId = classId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
        }

        public string Id { get; }
        public string Name { get; }
        public TeamSide Side { get; }
        public RowPosition Row { get; }
        public string RaceId { get; }
        public string ClassId { get; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; }
        public bool IsDefending { get; set; }

        public void TakeDamage(float amount)
        {
            CurrentHealth = UnityEngine.Mathf.Max(0f, CurrentHealth - amount);
        }

        public void Heal(float amount)
        {
            CurrentHealth = UnityEngine.Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        public BattleReplayActorSnapshot ToSnapshot()
        {
            return new BattleReplayActorSnapshot(
                Id,
                Name,
                Side,
                Row,
                RaceId,
                ClassId,
                CurrentHealth,
                MaxHealth,
                CurrentHealth > 0f);
        }
    }
}

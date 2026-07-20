using SM.Combat.Model;
using SM.Core.Contracts;

internal sealed class PackPursuitSurvivalObserver
{
    private const string PackPursuitBehaviorTag = "pack_pursuit";
    private readonly HashSet<string> _targetedShooterIds = new(StringComparer.Ordinal);
    private int _backlineDiveKillCount;

    internal void Observe(BattleState state, BattleSimulationStep step)
    {
        foreach (var diver in state.Enemies.Where(HasPackPursuitTag))
        {
            if (diver.CurrentCombatIntent.Type == CombatIntentType.Dive
                && diver.CurrentCombatIntent.TargetId is { } targetId)
            {
                RecordTarget(state.FindUnit(targetId));
            }
        }

        foreach (var kill in step.Events.Where(value => value.KillPayload?.IsBacklineDiveKill == true))
        {
            var diver = state.FindUnit(kill.ActorId);
            if (diver == null || !HasPackPursuitTag(diver))
            {
                continue;
            }

            var target = state.FindUnit(kill.TargetId);
            if (!IsBacklineShooter(target))
            {
                continue;
            }

            RecordTarget(target);
            _backlineDiveKillCount++;
        }
    }

    internal HeadlessCampaignFlankSurvival Complete(BattleState state)
    {
        var surviving = _targetedShooterIds.Count(id => state.FindUnitById(id) is { IsAlive: true });
        return new HeadlessCampaignFlankSurvival(
            _targetedShooterIds.Count,
            surviving,
            _backlineDiveKillCount);
    }

    private void RecordTarget(UnitSnapshot? target)
    {
        if (IsBacklineShooter(target))
        {
            _targetedShooterIds.Add(target!.Id.Value);
        }
    }

    private static bool IsBacklineShooter(UnitSnapshot? unit)
        => unit != null
           && unit.Side == TeamSide.Ally
           && unit.Behavior.FormationLine == FormationLine.Backline
           && unit.Definition.ClassId is "ranger" or "mystic";

    private static bool HasPackPursuitTag(UnitSnapshot unit)
        => (unit.Definition.RulePackages ?? Array.Empty<CombatRuleModifierPackage>())
            .SelectMany(package => package.Modifiers ?? Array.Empty<RuleModifier>())
            .Any(modifier => modifier.Kind == RuleModifierKind.BehaviorTag
                             && string.Equals(
                                 modifier.Value,
                                 PackPursuitBehaviorTag,
                                 StringComparison.Ordinal));
}

using SM.Combat.Model;

internal sealed class AntiClusterAoeSurvivalObserver
{
    private const string BossArchetypeId = "sunken_adjudicator_boss";
    private readonly HashSet<int> _castSteps = new();
    private int _aoeCatchCount;
    private int _shooterAoeCatchCount;

    internal void Observe(BattleState state, BattleSimulationStep step)
    {
        foreach (var battleEvent in step.Events)
        {
            if (battleEvent.LogCode != BattleLogCode.ActiveSkillDamage
                || battleEvent.TargetId is not { } targetId
                || !battleEvent.Note.Contains("GroundAoe", StringComparison.Ordinal))
            {
                continue;
            }

            var actor = state.FindUnit(battleEvent.ActorId);
            if (actor == null
                || !string.Equals(actor.Definition.ArchetypeId, BossArchetypeId, StringComparison.Ordinal))
            {
                continue;
            }

            var target = state.FindUnit(targetId);
            if (target == null || target.Side != TeamSide.Ally)
            {
                continue;
            }

            _castSteps.Add(step.StepIndex);
            _aoeCatchCount++;
            if (string.Equals(target.Definition.ClassId, "ranger", StringComparison.Ordinal))
            {
                _shooterAoeCatchCount++;
            }
        }
    }

    internal HeadlessCampaignAoeSurvival Complete(BattleState state)
    {
        var shooters = state.AllUnits
            .Where(unit => unit.Side == TeamSide.Ally
                           && string.Equals(unit.Definition.ClassId, "ranger", StringComparison.Ordinal))
            .ToArray();
        return new HeadlessCampaignAoeSurvival(
            _castSteps.Count,
            _aoeCatchCount,
            _shooterAoeCatchCount,
            shooters.Length,
            shooters.Count(unit => unit.IsAlive));
    }
}

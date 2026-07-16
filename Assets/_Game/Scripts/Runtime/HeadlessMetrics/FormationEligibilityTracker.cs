using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.HeadlessMetrics;

/// <summary>
/// 재실행 중 실제 BattleState predicate를 읽어 채널별 상황 성립을 누적한다. onStep 관찰만 수행하며
/// sim 상태나 RNG를 쓰지 않는다.
/// </summary>
public sealed class FormationEligibilityTracker
{
    private readonly HashSet<string> _eligible = new(StringComparer.Ordinal);

    public IReadOnlyList<string> EligibleChannelIds
        => _eligible.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public bool IsEligible(string channelId) => _eligible.Contains(channelId);

    public void Observe(BattleState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var allies = LivingRoster(state, TeamSide.Ally);
        var enemies = LivingRoster(state, TeamSide.Enemy);
        ObserveFlankArcs(state, allies, enemies);
        ObserveScreening(state, allies, enemies);
        ObserveSaveWindow(allies);
        ObserveDiveWindow(allies, enemies);
    }

    private void ObserveFlankArcs(
        BattleState state,
        IReadOnlyList<UnitSnapshot> allies,
        IReadOnlyList<UnitSnapshot> enemies)
    {
        foreach (var attacker in allies)
        {
            foreach (var target in enemies)
            {
                var arc = BattleFormationConsequence.ResolveFlankArc(state, attacker, target);
                if (!arc.IsFlanking)
                {
                    continue;
                }

                _eligible.Add(string.Equals(arc.NoteToken, "rear", StringComparison.Ordinal)
                    ? FormationChannelIds.Rear
                    : FormationChannelIds.Flank);
            }
        }
    }

    private void ObserveScreening(
        BattleState state,
        IReadOnlyList<UnitSnapshot> allies,
        IReadOnlyList<UnitSnapshot> enemies)
    {
        foreach (var attacker in enemies)
        {
            if (allies.Any(target => BattleFormationConsequence.IsScreenedBacklineFrom(state, attacker, target)))
            {
                _eligible.Add(FormationChannelIds.ScreenBlock);
                return;
            }
        }
    }

    private void ObserveSaveWindow(IReadOnlyList<UnitSnapshot> allies)
    {
        var hasLivingHealer = allies.Any(unit => unit.Definition.EffectiveSignatureActive?.Kind == SkillKind.Heal
                                                 || unit.Definition.EffectiveFlexActive?.Kind == SkillKind.Heal);
        if (hasLivingHealer && allies.Any(unit => unit.HealthRatio < CombatActionResolver.SaveMomentHealthRatio))
        {
            _eligible.Add(FormationChannelIds.Save);
        }
    }

    private void ObserveDiveWindow(
        IReadOnlyList<UnitSnapshot> allies,
        IReadOnlyList<UnitSnapshot> enemies)
    {
        if (enemies.Any(unit => unit.Behavior.FormationLine == FormationLine.Backline)
            && allies.Any(unit => unit.CurrentCombatIntent.Type == CombatIntentType.Dive))
        {
            _eligible.Add(FormationChannelIds.BacklineDiveKill);
        }
    }

    private static UnitSnapshot[] LivingRoster(BattleState state, TeamSide side)
        => state.GetTeam(side)
            .Where(unit => unit.IsAlive && unit.EntityKind == CombatEntityKind.RosterUnit)
            .ToArray();
}

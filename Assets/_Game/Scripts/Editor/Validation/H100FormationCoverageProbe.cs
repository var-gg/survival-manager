using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

/// <summary>
/// CoveragePolicy의 발동 가능성만 검증하는 통제된 개전 접촉이다. authored 전투를 조립한 뒤 위치/HP/의도를
/// 공개된 전투 상태로 구성하고 production resolver를 한 번 통과시킨다. Competent 자연 발생 표본에는 쓰지 않는다.
/// </summary>
internal static class H100FormationCoverageProbe
{
    private const int MaximumContactAttempts = 8;

    public static IReadOnlyList<BattleEvent> Run(
        BattleState state,
        string channelId,
        Action observeEligibility)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return Array.Empty<BattleEvent>();
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (observeEligibility == null)
        {
            throw new ArgumentNullException(nameof(observeEligibility));
        }

        var before = Count(state, channelId);
        var events = new List<BattleEvent>();
        for (var attempt = 0; attempt < MaximumContactAttempts && Count(state, channelId) <= before; attempt++)
        {
            events.AddRange(channelId switch
            {
                FormationChannelIds.Flank => ResolveFlankContact(state, rear: false, observeEligibility),
                FormationChannelIds.Rear => ResolveFlankContact(state, rear: true, observeEligibility),
                FormationChannelIds.ScreenBlock => ResolveScreenContact(state, observeEligibility),
                FormationChannelIds.Save => ResolveSaveContact(state, observeEligibility),
                FormationChannelIds.BacklineDiveKill => ResolveDiveContact(state, observeEligibility),
                _ => throw new InvalidOperationException($"Unknown formation coverage channel: {channelId}"),
            });
            if (Count(state, channelId) <= before)
            {
                // Hit dodge is a deterministic hash of seed+step+ids. A bounded QA probe advances only its
                // synthetic opening step before retrying so it does not repeat the identical miss forever.
                state.AdvanceStep();
            }
        }

        var after = Count(state, channelId);
        if (after <= before)
        {
            throw new InvalidOperationException(
                $"Formation coverage probe did not fire typed channel {channelId}: before={before}, after={after}.");
        }

        return events.ToArray();
    }

    private static IReadOnlyList<BattleEvent> ResolveFlankContact(
        BattleState state,
        bool rear,
        Action observeEligibility)
    {
        var attacker = Roster(state, TeamSide.Ally)
            .OrderBy(unit => unit.Definition.ClassId == "duelist" ? 0 : 1)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .First();
        var target = Roster(state, TeamSide.Enemy)
            .OrderBy(unit => unit.Behavior.FormationLine)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .First();
        target.SetCurrentTarget(null);
        target.SetPosition(new CombatVector2(0f, 0f));
        attacker.SetPosition(rear ? new CombatVector2(0.8f, 0f) : new CombatVector2(0f, 0.8f));
        observeEligibility();
        return ResolveBasicAttack(state, attacker, target);
    }

    private static IReadOnlyList<BattleEvent> ResolveScreenContact(
        BattleState state,
        Action observeEligibility)
    {
        var target = Roster(state, TeamSide.Ally)
            .Where(unit => unit.Behavior.FormationLine == FormationLine.Backline)
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Coverage screen probe requires an allied backline unit.");
        var guard = Roster(state, TeamSide.Ally)
            .Where(unit => unit.Id != target.Id && unit.Behavior.FormationLine == FormationLine.Frontline)
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Coverage screen probe requires an allied frontline guard.");
        var attacker = Roster(state, TeamSide.Enemy)
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .First();
        target.SetPosition(new CombatVector2(-1f, 0f));
        guard.SetPosition(new CombatVector2(-0.2f, 0f));
        attacker.SetPosition(new CombatVector2(0.6f, 0f));
        observeEligibility();
        return ResolveBasicAttack(state, attacker, target);
    }

    private static IReadOnlyList<BattleEvent> ResolveSaveContact(
        BattleState state,
        Action observeEligibility)
    {
        var healer = Roster(state, TeamSide.Ally)
            .Select(unit => new { Unit = unit, Skill = ResolveHealSkill(unit) })
            .Where(value => value.Skill != null)
            .OrderBy(value => value.Unit.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Coverage save probe requires an allied healer.");
        var target = Roster(state, TeamSide.Ally)
            .Where(unit => unit.Id != healer.Unit.Id)
            .OrderByDescending(unit => unit.MaxHealth)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .First();
        target.TakeDamage(Math.Max(
            0f,
            target.Barrier + target.CurrentHealth - (target.MaxHealth * 0.20f)));
        observeEligibility();
        healer.Unit.BeginWindup(BattleActionType.ActiveSkill, target.Id, healer.Skill!.Id);
        return CombatActionResolver.Resolve(state, healer.Unit);
    }

    private static IReadOnlyList<BattleEvent> ResolveDiveContact(
        BattleState state,
        Action observeEligibility)
    {
        var diver = Roster(state, TeamSide.Ally)
            .Where(unit => string.Equals(unit.Definition.ClassId, "duelist", StringComparison.Ordinal))
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Coverage dive probe requires an allied duelist.");
        var target = Roster(state, TeamSide.Enemy)
            .Where(unit => unit.Behavior.FormationLine == FormationLine.Backline)
            .OrderBy(unit => unit.CurrentHealth)
            .ThenBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Coverage dive probe requires an enemy backline unit.");
        diver.SetPosition(new CombatVector2(0f, 0f));
        target.SetPosition(new CombatVector2(0.8f, 0f));
        target.TakeDamage(Math.Max(0f, target.Barrier + target.CurrentHealth - 1f));
        diver.SetCombatIntent(new CombatIntent(
            CombatIntentType.Dive,
            target.Id,
            null,
            diver.Position,
            state.StepIndex + 12,
            100));
        observeEligibility();
        return ResolveBasicAttack(state, diver, target);
    }

    private static IReadOnlyList<BattleEvent> ResolveBasicAttack(
        BattleState state,
        UnitSnapshot attacker,
        UnitSnapshot target)
    {
        attacker.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        return CombatActionResolver.Resolve(state, attacker);
    }

    private static BattleSkillSpec? ResolveHealSkill(UnitSnapshot unit)
    {
        if (unit.EffectiveFlexActive?.Kind == SkillKind.Heal)
        {
            return unit.EffectiveFlexActive;
        }

        return unit.EffectiveSignatureActive?.Kind == SkillKind.Heal
            ? unit.EffectiveSignatureActive
            : null;
    }

    private static UnitSnapshot[] Roster(BattleState state, TeamSide side)
        => state.GetTeam(side)
            .Where(unit => unit.IsAlive && unit.EntityKind == CombatEntityKind.RosterUnit)
            .ToArray();

    private static int Count(BattleState state, string channelId)
        => channelId switch
        {
            FormationChannelIds.Flank => Math.Max(
                0,
                state.ActivityTelemetry.FlankStrikeCount - state.ActivityTelemetry.RearStrikeCount),
            FormationChannelIds.Rear => state.ActivityTelemetry.RearStrikeCount,
            FormationChannelIds.ScreenBlock => state.ActivityTelemetry.ScreenAbsorbCount
                                                    + state.ActivityTelemetry.ScreenDeterrenceCount,
            FormationChannelIds.Save => state.ActivityTelemetry.SaveMomentCount,
            FormationChannelIds.BacklineDiveKill => state.ActivityTelemetry.BacklineDiveKillCount,
            _ => 0,
        };
}

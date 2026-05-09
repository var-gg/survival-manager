using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class FormationTacticTests
{
    [Test]
    public void TacticContext_NormalizesDials_AndIsSharedWithinStep()
    {
        var tactic = new TeamTacticProfile(
            "extreme",
            "Extreme",
            TeamPostureType.CollapseWeakSide,
            CombatPace: 2.4f,
            FocusModeBias: 2f,
            FrontSpacingBias: 2f,
            BackSpacingBias: -1f,
            ProtectCarryBias: 1.4f,
            TargetSwitchPenalty: 4f,
            Compactness: -0.5f,
            Width: 3f,
            Depth: 0.1f,
            LineSpacing: 2f,
            FlankBias: -2f);
        var state = BattleFactory.Create(
            new[] { CreateUnit("ally", DeploymentAnchorId.FrontCenter, tactic) },
            new[] { CreateUnit("enemy", DeploymentAnchorId.FrontCenter, tactic) },
            seed: 11);

        var first = state.GetTacticContext(TeamSide.Ally);
        var second = state.GetTacticContext(TeamSide.Ally);

        Assert.That(ReferenceEquals(first, second), Is.True);
        Assert.That(first.Posture, Is.EqualTo(TeamPostureType.CollapseWeakSide));
        Assert.That(first.CombatPace, Is.EqualTo(1.35f).Within(0.001f));
        Assert.That(first.FocusModeBias, Is.EqualTo(1f).Within(0.001f));
        Assert.That(first.FrontSpacingBias, Is.EqualTo(1f).Within(0.001f));
        Assert.That(first.BackSpacingBias, Is.EqualTo(0f).Within(0.001f));
        Assert.That(first.Width, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(first.Depth, Is.EqualTo(0.35f).Within(0.001f));
        Assert.That(first.FlankBias, Is.EqualTo(-1f).Within(0.001f));

        state.AdvanceStep();

        Assert.That(ReferenceEquals(first, state.GetTacticContext(TeamSide.Ally)), Is.False);
    }

    [Test]
    public void BattleFactory_TacticDialsChangeStartingOccupiedArea()
    {
        var compact = new TeamTacticProfile(
            "compact",
            "Compact",
            TeamPostureType.ProtectCarry,
            Compactness: 0.9f,
            Width: 0.82f,
            Depth: 0.82f,
            LineSpacing: 0.78f,
            ProtectCarryBias: 0.45f);
        var spread = new TeamTacticProfile(
            "spread",
            "Spread",
            TeamPostureType.StandardAdvance,
            Compactness: 0.08f,
            Width: 1.32f,
            Depth: 1.18f,
            LineSpacing: 1.16f,
            FlankBias: 0.32f);

        var compactState = BattleFactory.Create(CreateFourUnitTeam(compact), CreateFourUnitTeam(compact), seed: 31);
        var spreadState = BattleFactory.Create(CreateFourUnitTeam(spread), CreateFourUnitTeam(spread), seed: 31);

        Assert.That(OccupiedArea(spreadState.Allies), Is.GreaterThan(OccupiedArea(compactState.Allies) * 1.5f));
        Assert.That(SpanY(spreadState.Allies), Is.GreaterThan(SpanY(compactState.Allies) * 1.35f));
        Assert.That(compactState.AllyPosture, Is.EqualTo(TeamPostureType.ProtectCarry));
    }

    [Test]
    public void BattleFactory_RoleInstructionAddsDeterministicLocalOffset_ForSameAnchor()
    {
        var tactic = new TeamTacticProfile(
            "balanced",
            "Balanced",
            TeamPostureType.StandardAdvance,
            Compactness: 0.45f,
            Width: 1f,
            Depth: 1f,
            LineSpacing: 1f);
        var anchor = CreateUnit(
            "anchor",
            DeploymentAnchorId.FrontCenter,
            tactic,
            new SlotRoleInstruction(DeploymentAnchorId.FrontCenter, "anchor", ProtectCarryBias: 0.45f),
            RoleVariantTag.Peeler);
        var diver = CreateUnit(
            "diver",
            DeploymentAnchorId.FrontCenter,
            tactic,
            new SlotRoleInstruction(DeploymentAnchorId.FrontCenter, "diver", BacklinePressureBias: 0.55f),
            RoleVariantTag.Diver,
            "duelist");

        var first = BattleFactory.Create(new[] { anchor, diver }, new[] { CreateUnit("enemy", DeploymentAnchorId.FrontCenter, tactic) }, seed: 43);
        var second = BattleFactory.Create(new[] { anchor, diver }, new[] { CreateUnit("enemy", DeploymentAnchorId.FrontCenter, tactic) }, seed: 43);

        var firstAnchor = first.Allies.Single(unit => unit.Definition.Id == "anchor");
        var firstDiver = first.Allies.Single(unit => unit.Definition.Id == "diver");
        var secondAnchor = second.Allies.Single(unit => unit.Definition.Id == "anchor");
        var secondDiver = second.Allies.Single(unit => unit.Definition.Id == "diver");

        Assert.That(firstAnchor.Position.DistanceTo(firstDiver.Position), Is.GreaterThan(0.18f));
        Assert.That(secondAnchor.Position.X, Is.EqualTo(firstAnchor.Position.X).Within(0.001f));
        Assert.That(secondAnchor.Position.Y, Is.EqualTo(firstAnchor.Position.Y).Within(0.001f));
        Assert.That(secondDiver.Position.X, Is.EqualTo(firstDiver.Position.X).Within(0.001f));
        Assert.That(secondDiver.Position.Y, Is.EqualTo(firstDiver.Position.Y).Within(0.001f));
    }

    [Test]
    public void BattleFactory_RecordsFormationIntentTelemetry_ForQa()
    {
        var tactic = new TeamTacticProfile(
            "flank",
            "Flank",
            TeamPostureType.CollapseWeakSide,
            Compactness: 0.2f,
            Width: 1.18f,
            Depth: 1.05f,
            LineSpacing: 1.05f,
            FlankBias: 0.45f);
        var unit = CreateUnit(
            "raider",
            DeploymentAnchorId.FrontTop,
            tactic,
            new SlotRoleInstruction(DeploymentAnchorId.FrontTop, "diver", BacklinePressureBias: 0.5f),
            RoleVariantTag.Diver,
            "duelist");

        var state = BattleFactory.Create(new[] { unit }, new[] { CreateUnit("enemy", DeploymentAnchorId.FrontCenter, tactic) }, seed: 7);

        var formationRecord = state.TelemetryEvents.Single(record =>
            record.EventKind == TelemetryEventKind.PositioningIntentUpdated
            && record.Actor?.UnitBlueprintId == "raider"
            && record.StringValueA == "formation:flank");
        Assert.That(formationRecord.StringValueB, Does.Contain("FrontTop"));
        Assert.That(formationRecord.StringValueB, Does.Contain("diver"));
        Assert.That(formationRecord.ValueA, Is.Not.EqualTo(0f));
        Assert.That(formationRecord.ValueB, Is.Not.EqualTo(0f));
    }

    [Test]
    public void MovementResolver_ZoneCandidateUsesTacticWidth_WhenHomeIsBlocked()
    {
        var compact = new TeamTacticProfile(
            "compact_zone",
            "Compact Zone",
            TeamPostureType.StandardAdvance,
            Compactness: 0.9f,
            Width: 0.55f,
            Depth: 0.65f);
        var wide = compact with
        {
            Id = "wide_zone",
            DisplayName = "Wide Zone",
            Compactness = 0f,
            Width = 1.45f,
            Depth = 1.2f,
        };
        var compactState = BattleFactory.Create(
            new[]
            {
                CreateUnit("actor", DeploymentAnchorId.FrontCenter, compact),
                CreateUnit("blocker", DeploymentAnchorId.FrontCenter, compact),
            },
            new[] { CreateUnit("enemy", DeploymentAnchorId.FrontCenter, compact) },
            seed: 19);
        var wideState = BattleFactory.Create(
            new[]
            {
                CreateUnit("actor", DeploymentAnchorId.FrontCenter, wide),
                CreateUnit("blocker", DeploymentAnchorId.FrontCenter, wide),
            },
            new[] { CreateUnit("enemy", DeploymentAnchorId.FrontCenter, wide) },
            seed: 19);
        var compactActor = compactState.Allies.Single(unit => unit.Definition.Id == "actor");
        var wideActor = wideState.Allies.Single(unit => unit.Definition.Id == "actor");
        var compactIntent = compactActor.AnchorPosition + new CombatVector2(0.45f, 0f);
        var wideIntent = wideActor.AnchorPosition + new CombatVector2(0.45f, 0f);
        compactState.Allies.Single(unit => unit.Definition.Id == "blocker").SetPosition(compactIntent);
        wideState.Allies.Single(unit => unit.Definition.Id == "blocker").SetPosition(wideIntent);

        var compactHome = MovementResolver.ResolveHomePosition(compactState, compactActor);
        var wideHome = MovementResolver.ResolveHomePosition(wideState, wideActor);

        Assert.That(wideHome.DistanceTo(wideIntent), Is.GreaterThan(compactHome.DistanceTo(compactIntent) + 0.2f));
    }

    [Test]
    public void EngagementSlots_UseTacticSpread_AndRemainStickyUntilBlocked()
    {
        var compact = new TeamTacticProfile(
            "compact_slots",
            "Compact Slots",
            TeamPostureType.StandardAdvance,
            FrontSpacingBias: 0f,
            Compactness: 0.9f,
            Width: 0.55f);
        var wide = compact with
        {
            Id = "wide_slots",
            DisplayName = "Wide Slots",
            FrontSpacingBias = 1f,
            Compactness = 0f,
            Width = 1.45f,
            FlankBias = 0.4f,
        };

        var compactState = CreateSlotState(compact);
        var wideState = CreateSlotState(wide);
        var compactSlots = ResolveTwoSlots(compactState);
        var wideSlots = ResolveTwoSlots(wideState);

        Assert.That(wideSlots[0].Position.DistanceTo(wideSlots[1].Position), Is.GreaterThan(compactSlots[0].Position.DistanceTo(compactSlots[1].Position)));

        var stickyActor = wideState.Allies[0];
        stickyActor.SetEngagementSlot(wideSlots[0]);
        var sticky = EngagementSlotService.Resolve(
            wideState,
            stickyActor,
            wideState.Enemies[0],
            new FloatRange(0.6f, 1.2f),
            PositioningIntentKind.Frontline);

        Assert.That(sticky, Is.EqualTo(wideSlots[0]));
    }

    [Test]
    public void ActivityTelemetry_PostAttackRepositionAndReplayHash_AreDeterministic()
    {
        var tactic = new TeamTacticProfile(
            "wide_reposition",
            "Wide Reposition",
            TeamPostureType.StandardAdvance,
            FocusModeBias: 0.35f,
            FrontSpacingBias: 0.8f,
            Compactness: 0f,
            Width: 1.5f,
            Depth: 1.1f,
            FlankBias: 0.9f);

        var first = RunActivityScenario(tactic, 53);
        var second = RunActivityScenario(tactic, 53);
        var firstTelemetry = first.ActivityTelemetry!;
        var secondTelemetry = second.ActivityTelemetry!;

        Assert.That(firstTelemetry.PostAttackRepositionCount, Is.GreaterThan(0));
        Assert.That(secondTelemetry.PostAttackRepositionCount, Is.EqualTo(firstTelemetry.PostAttackRepositionCount));
        Assert.That(secondTelemetry.ReplayHash, Is.EqualTo(firstTelemetry.ReplayHash));
        Assert.That(first.TelemetryEvents!.Any(record => record.EventKind == TelemetryEventKind.ActivityMetricRecorded
                                                         && record.StringValueA == "ReplayHash"), Is.True);
    }

    [Test]
    public void ActivityTelemetry_PresetsProduceDifferentSpacingAndStationaryMetrics()
    {
        var compact = new TeamTacticProfile(
            "compact_focus",
            "Compact Focus",
            TeamPostureType.ProtectCarry,
            FocusModeBias: 0.8f,
            FrontSpacingBias: 0f,
            ProtectCarryBias: 0.55f,
            Compactness: 0.9f,
            Width: 0.55f,
            Depth: 0.65f);
        var wide = new TeamTacticProfile(
            "wide_spread",
            "Wide Spread",
            TeamPostureType.StandardAdvance,
            FocusModeBias: -0.6f,
            FrontSpacingBias: 1f,
            Compactness: 0f,
            Width: 1.45f,
            Depth: 1.25f,
            FlankBias: 0.6f);

        var compactTelemetry = RunActivityScenario(compact, 61).ActivityTelemetry!;
        var wideTelemetry = RunActivityScenario(wide, 61).ActivityTelemetry!;

        Assert.That(wideTelemetry.MeanPairwiseDistanceByTeam[TeamSide.Ally], Is.GreaterThan(compactTelemetry.MeanPairwiseDistanceByTeam[TeamSide.Ally]));
        Assert.That(wideTelemetry.TargetEntropy, Is.Not.EqualTo(compactTelemetry.TargetEntropy).Within(0.001f));
        Assert.That(wideTelemetry.StationaryBetweenAttacksRatio, Is.Not.EqualTo(compactTelemetry.StationaryBetweenAttacksRatio).Within(0.001f));
    }

    private static IReadOnlyList<BattleUnitLoadout> CreateFourUnitTeam(TeamTacticProfile tactic)
    {
        return new[]
        {
            CreateUnit("front_top", DeploymentAnchorId.FrontTop, tactic, roleVariant: RoleVariantTag.Peeler),
            CreateUnit("front_bottom", DeploymentAnchorId.FrontBottom, tactic, roleVariant: RoleVariantTag.Diver, classId: "duelist"),
            CreateUnit("back_top", DeploymentAnchorId.BackTop, tactic, roleVariant: RoleVariantTag.Sniper, classId: "ranger"),
            CreateUnit("back_bottom", DeploymentAnchorId.BackBottom, tactic, roleVariant: RoleVariantTag.Controller, classId: "mystic"),
        };
    }

    private static BattleUnitLoadout CreateUnit(
        string id,
        DeploymentAnchorId anchor,
        TeamTacticProfile tactic,
        SlotRoleInstruction? roleInstruction = null,
        RoleVariantTag roleVariant = RoleVariantTag.Anchor,
        string classId = "vanguard")
    {
        roleInstruction ??= new SlotRoleInstruction(anchor, roleVariant.ToString().ToLowerInvariant());
        return CombatTestFactory.CreateLoopAUnit(id, classId: classId, anchor: anchor) with
        {
            TeamTactic = tactic,
            RoleInstruction = roleInstruction,
            RoleTag = roleInstruction.RoleTag,
            RoleVariant = roleVariant,
        };
    }

    private static BattleState CreateSlotState(TeamTacticProfile tactic)
    {
        return BattleFactory.Create(
            new[]
            {
                CreateUnit("attacker_a", DeploymentAnchorId.FrontTop, tactic, roleVariant: RoleVariantTag.Anchor, classId: "vanguard"),
                CreateUnit("attacker_b", DeploymentAnchorId.FrontBottom, tactic, roleVariant: RoleVariantTag.Anchor, classId: "vanguard"),
            },
            new[] { CreateUnit("target", DeploymentAnchorId.FrontCenter, tactic, roleVariant: RoleVariantTag.Anchor, classId: "vanguard") },
            seed: 23);
    }

    private static EngagementSlotAssignment[] ResolveTwoSlots(BattleState state)
    {
        var band = new FloatRange(0.6f, 1.2f);
        foreach (var ally in state.Allies)
        {
            ally.SetCurrentTarget(state.Enemies[0].Id);
        }

        return state.Allies
            .Select(ally => EngagementSlotService.Resolve(state, ally, state.Enemies[0], band, PositioningIntentKind.Frontline)!)
            .ToArray();
    }

    private static BattleResult RunActivityScenario(TeamTacticProfile tactic, int seed)
    {
        var allies = new[]
        {
            CreateUnit("ally_anchor", DeploymentAnchorId.FrontTop, tactic, roleVariant: RoleVariantTag.Anchor, classId: "vanguard") with { BaseStats = CombatTestFactory.CreateLoopAUnit("ally_anchor").BaseStats },
            CreateUnit("ally_duelist", DeploymentAnchorId.FrontBottom, tactic, roleVariant: RoleVariantTag.Diver, classId: "duelist"),
            CreateUnit("ally_ranger", DeploymentAnchorId.BackTop, tactic, roleVariant: RoleVariantTag.Sniper, classId: "ranger"),
        };
        var enemies = new[]
        {
            CreateUnit("enemy_anchor", DeploymentAnchorId.FrontTop, tactic, roleVariant: RoleVariantTag.Anchor, classId: "vanguard") with { BaseStats = CombatTestFactory.CreateLoopAUnit("enemy_anchor", race: "undead", hp: 160f, physPower: 2f, armor: 3f).BaseStats },
            CreateUnit("enemy_duelist", DeploymentAnchorId.FrontBottom, tactic, roleVariant: RoleVariantTag.Diver, classId: "duelist"),
            CreateUnit("enemy_ranger", DeploymentAnchorId.BackTop, tactic, roleVariant: RoleVariantTag.Sniper, classId: "ranger"),
        };
        var state = BattleFactory.Create(allies, enemies, seed: seed);
        return new BattleSimulator(state, 180).RunToEnd();
    }

    private static float OccupiedArea(IReadOnlyList<UnitSnapshot> units)
    {
        return SpanX(units) * SpanY(units);
    }

    private static float SpanX(IReadOnlyList<UnitSnapshot> units)
    {
        return units.Max(unit => unit.Position.X) - units.Min(unit => unit.Position.X);
    }

    private static float SpanY(IReadOnlyList<UnitSnapshot> units)
    {
        return units.Max(unit => unit.Position.Y) - units.Min(unit => unit.Position.Y);
    }
}

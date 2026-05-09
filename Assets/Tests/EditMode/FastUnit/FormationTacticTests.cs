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

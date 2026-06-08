using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using UnityEngine;

namespace SM.Unity;

public readonly record struct BattleCameraSuggestedFrame(
    Vector3 GroundCenter,
    float ZoomHeight,
    bool IsBootstrap);

public sealed class BattleCameraFramingPolicy
{
    private const float EmptyFrameZoomHeight = 7.7f;
    private const float BaseZoomHeight = 5.05f;
    private const float MaxBootstrapZoomHeight = 10.8f;
    private const float MaxUnitFocusZoomHeight = 6.4f;

    public BattleCameraSuggestedFrame BuildBootstrapFrame(BattleSimulationStep step, string selectedUnitId = "")
    {
        var points = CollectBootstrapPoints(step, selectedUnitId);
        return BuildFrame(points, isBootstrap: true);
    }

    public BattleCameraSuggestedFrame BuildPassiveFrame(BattleSimulationStep step, string selectedUnitId = "")
    {
        var points = CollectPassivePoints(step, selectedUnitId);
        return BuildFrame(points, isBootstrap: false);
    }

    public BattleCameraSuggestedFrame BuildUnitFocusFrame(BattleSimulationStep step, string selectedUnitId)
    {
        var selected = step.Units.FirstOrDefault(unit => unit.Id == selectedUnitId);
        if (selected == null)
        {
            return BuildPassiveFrame(step, selectedUnitId);
        }

        var points = new List<Vector3> { ToWorld(selected.Position) };
        AddUnitPoint(step, points, selected.TargetId);
        var frame = BuildFrame(points, isBootstrap: true);
        return new BattleCameraSuggestedFrame(frame.GroundCenter, Mathf.Clamp(frame.ZoomHeight, 5.0f, MaxUnitFocusZoomHeight), true);
    }

    private static BattleCameraSuggestedFrame BuildFrame(IReadOnlyList<Vector3> points, bool isBootstrap)
    {
        if (points.Count == 0)
        {
            return new BattleCameraSuggestedFrame(Vector3.zero, EmptyFrameZoomHeight, isBootstrap);
        }

        var minX = points.Min(point => point.x);
        var maxX = points.Max(point => point.x);
        var minZ = points.Min(point => point.z);
        var maxZ = points.Max(point => point.z);

        var center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        var extentX = maxX - minX;
        var extentZ = maxZ - minZ;
        var zoom = Mathf.Clamp(BaseZoomHeight + (extentX * 0.42f) + (extentZ * 0.76f), 5.0f, MaxBootstrapZoomHeight);
        return new BattleCameraSuggestedFrame(center, zoom, isBootstrap);
    }

    private static IReadOnlyList<Vector3> CollectBootstrapPoints(BattleSimulationStep step, string selectedUnitId)
    {
        var points = new List<Vector3>();
        foreach (TeamSide side in System.Enum.GetValues(typeof(TeamSide)))
        {
            foreach (DeploymentAnchorId anchor in System.Enum.GetValues(typeof(DeploymentAnchorId)))
            {
                points.Add(ToWorld(BattleFactory.ResolveAnchorPosition(side, anchor)));
            }
        }

        points.AddRange(step.Units.Where(unit => unit.IsAlive).Select(unit => ToWorld(unit.Position)));
        if (!string.IsNullOrWhiteSpace(selectedUnitId)
            && step.Units.FirstOrDefault(unit => unit.Id == selectedUnitId) is { } selected)
        {
            points.Add(ToWorld(selected.Position));
        }

        return points;
    }

    private static IReadOnlyList<Vector3> CollectPassivePoints(BattleSimulationStep step, string selectedUnitId)
    {
        var points = step.Units
            .Where(unit => unit.IsAlive)
            .Select(unit => ToWorld(unit.Position))
            .ToList();

        if (BattleReadabilityFormatter.TryResolveStepFocus(step, out var focus))
        {
            AddUnitPoint(step, points, focus.ActorId);
            if (!string.IsNullOrWhiteSpace(focus.TargetId))
            {
                AddUnitPoint(step, points, focus.TargetId);
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedUnitId))
        {
            AddUnitPoint(step, points, selectedUnitId);
        }

        return points;
    }

    private static void AddUnitPoint(BattleSimulationStep step, ICollection<Vector3> points, string? unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId))
        {
            return;
        }

        if (step.Units.FirstOrDefault(unit => unit.Id == unitId) is { } unit)
        {
            points.Add(ToWorld(unit.Position));
        }
    }

    private static Vector3 ToWorld(CombatVector2 position) => new(position.X, 0f, position.Y);
}

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
    private const float MaxCloseShotZoomHeight = 5.6f;

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

    /// <summary>
    /// Phase 4 디렉터 강조 프레임 — 사건 당사자(주체/상대) 2점 AABB 로 타이트하게 프레임한다.
    /// 유닛이 해석 안 되면 beat 발생 지점으로. 항상 비-bootstrap(블렌드 제안)이라 하드 스냅이 없고
    /// 수동 카메라 우선권(ManualHold)도 기존 SetSuggestedFrame 경로에서 그대로 보존된다.
    /// </summary>
    public BattleCameraSuggestedFrame BuildEmphasisFrame(BattleSimulationStep step, SpectacleCameraEmphasis emphasis)
    {
        var points = new List<Vector3>(2);
        AddUnitPoint(step, points, emphasis.SubjectUnitId);
        AddUnitPoint(step, points, emphasis.RelatedUnitId);
        if (points.Count == 0)
        {
            points.Add(ToWorld(emphasis.FallbackPosition));
        }

        var frame = BuildFrame(points, isBootstrap: false);
        var maxZoom = emphasis.ShotKind == SpectacleShotKind.Close ? MaxCloseShotZoomHeight : MaxUnitFocusZoomHeight;
        return frame with { ZoomHeight = Mathf.Clamp(frame.ZoomHeight, 5.0f, maxZoom) };
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

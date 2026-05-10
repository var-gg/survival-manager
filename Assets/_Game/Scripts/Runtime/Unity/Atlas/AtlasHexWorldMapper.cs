using System;
using SM.Atlas.Model;
using UnityEngine;

namespace SM.Unity.Atlas;

public static class AtlasHexWorldMapper
{
    public const float HexRadius = 0.72f;
    public const float HorizontalStep = 1.28f;
    public const float VerticalStep = 1.08f;
    public const float SurfaceY = 0.08f;

    public static Vector3 ToWorld(AtlasHexCoordinate hex)
    {
        var x = (hex.Q * HorizontalStep) + (hex.R * HorizontalStep * 0.5f);
        var z = hex.R * VerticalStep;
        return new Vector3(x, SurfaceY, z);
    }

    public static Vector3[] BuildHexCorners(Vector3 center, float radius = HexRadius, float yOffset = 0f)
    {
        var corners = new Vector3[6];
        for (var i = 0; i < corners.Length; i++)
        {
            var radians = Mathf.Deg2Rad * (60f * i + 30f);
            corners[i] = new Vector3(
                center.x + Mathf.Cos(radians) * radius,
                center.y + yOffset,
                center.z + Mathf.Sin(radians) * radius);
        }

        return corners;
    }

    public static Mesh CreateHexDiscMesh(float radius = HexRadius, float yOffset = 0f)
    {
        var vertices = new Vector3[7];
        vertices[0] = new Vector3(0f, yOffset, 0f);
        var corners = BuildHexCorners(Vector3.zero, radius, yOffset);
        Array.Copy(corners, 0, vertices, 1, corners.Length);

        var triangles = new int[18];
        for (var i = 0; i < 6; i++)
        {
            var offset = i * 3;
            var next = i == 5 ? 1 : i + 2;
            triangles[offset] = 0;
            triangles[offset + 1] = next;
            triangles[offset + 2] = i + 1;
        }

        var mesh = new Mesh
        {
            name = $"AtlasHexDisc_r{radius:0.00}",
            vertices = vertices,
            triangles = triangles,
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh CreateHexRingMesh(float outerRadius = HexRadius, float thickness = 0.035f, float yOffset = 0f)
    {
        var innerRadius = Mathf.Max(0.01f, outerRadius - thickness);
        var vertices = new Vector3[12];
        var outer = BuildHexCorners(Vector3.zero, outerRadius, yOffset);
        var inner = BuildHexCorners(Vector3.zero, innerRadius, yOffset);
        Array.Copy(outer, 0, vertices, 0, 6);
        Array.Copy(inner, 0, vertices, 6, 6);

        var triangles = new int[36];
        for (var i = 0; i < 6; i++)
        {
            var next = i == 5 ? 0 : i + 1;
            var offset = i * 6;
            triangles[offset] = i;
            triangles[offset + 1] = 6 + i;
            triangles[offset + 2] = next;
            triangles[offset + 3] = next;
            triangles[offset + 4] = 6 + i;
            triangles[offset + 5] = 6 + next;
        }

        var mesh = new Mesh
        {
            name = $"AtlasHexRing_r{outerRadius:0.00}",
            vertices = vertices,
            triangles = triangles,
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

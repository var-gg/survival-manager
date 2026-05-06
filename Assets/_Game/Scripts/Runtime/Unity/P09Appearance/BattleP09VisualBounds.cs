using System;
using UnityEngine;

namespace SM.Unity
{

internal static class BattleP09VisualBounds
{
    private static readonly string[] EquipmentExtentTokens =
    {
        "Weapon",
        "Sword",
        "Bow",
        "Arrow",
        "Shield",
        "Staff",
        "Axe",
        "Spear",
        "Dagger",
        "Gun",
        "Wand",
    };

    public static bool TryCalculateStableHumanoidBounds(Transform root, out Bounds bounds)
    {
        return TryCalculateBounds(root, excludeExtendedEquipment: true, out bounds)
               || TryCalculateBounds(root, excludeExtendedEquipment: false, out bounds);
    }

    private static bool TryCalculateBounds(Transform root, bool excludeExtendedEquipment, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        var hasBounds = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (excludeExtendedEquipment && IsExtendedEquipmentRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool IsExtendedEquipmentRenderer(Renderer renderer)
    {
        if (ContainsExtendedEquipmentToken(renderer.name))
        {
            return true;
        }

        var transform = renderer.transform;
        while (transform != null)
        {
            if (ContainsExtendedEquipmentToken(transform.name))
            {
                return true;
            }

            transform = transform.parent;
        }

        foreach (var material in renderer.sharedMaterials)
        {
            if (material != null && ContainsExtendedEquipmentToken(material.name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsExtendedEquipmentToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (var i = 0; i < EquipmentExtentTokens.Length; i++)
        {
            if (value.Contains(EquipmentExtentTokens[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

}

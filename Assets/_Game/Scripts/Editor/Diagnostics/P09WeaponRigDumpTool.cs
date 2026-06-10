using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;

namespace SM.Editor.Diagnostics;

/// <summary>
/// 활/지팡이 "등에 맨 채 공격" 진단용 일회성 덤프. 게임이 실제로 스폰하는 P09 비주얼 프리팹을
/// 에디터에서 임시 인스턴스화해 무기 오브젝트 계층 + ParentConstraint 소스/가중치/활성 상태를
/// 콘솔로 출력한다. 씬을 더럽히지 않도록 출력 후 즉시 파괴한다.
/// </summary>
public static class P09WeaponRigDumpTool
{
    private const string PrefabPath = "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Demo_Prefab/P09_Human_Combat_Demo Variant.prefab";

    [MenuItem("SM/Internal/Diagnostics/Dump P09 Weapon Rig")]
    public static void Dump()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[P09RigDump] prefab not found: {PrefabPath}");
            return;
        }

        var instance = Object.Instantiate(prefab);
        try
        {
            var sb = new StringBuilder(8192);
            sb.AppendLine($"[P09RigDump] root={instance.name}");

            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
            {
                var name = transform.name;
                var interesting = name.Contains("Target") || name.Contains("Bow") || name.Contains("Staff")
                    || name.Contains("Wep") || name.Contains("Weapon") || name.Contains("Shield")
                    || name.Contains("Arrow") || name.Contains("Quiver") || name.Contains("Sword");
                var constraint = transform.GetComponent<ParentConstraint>();
                if (!interesting && constraint == null)
                {
                    continue;
                }

                sb.Append(GetPath(transform, instance.transform));
                sb.Append($" | active={transform.gameObject.activeSelf}");
                var renderer = transform.GetComponent<Renderer>();
                if (renderer != null)
                {
                    sb.Append($" | renderer(enabled={renderer.enabled})");
                }

                if (constraint != null)
                {
                    sb.Append($" | ParentConstraint(active={constraint.constraintActive}, weight={constraint.weight:0.##}, locked={constraint.locked}, sources=[");
                    for (var i = 0; i < constraint.sourceCount; i++)
                    {
                        var source = constraint.GetSource(i);
                        var sourceName = source.sourceTransform != null ? source.sourceTransform.name : "<null>";
                        sb.Append($"{i}:{sourceName} w={source.weight:0.##}");
                        if (i < constraint.sourceCount - 1)
                        {
                            sb.Append(", ");
                        }
                    }

                    sb.Append("])");
                }

                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static string GetPath(Transform transform, Transform root)
    {
        if (transform == root)
        {
            return transform.name;
        }

        var path = transform.name;
        var current = transform.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return root.name + "/" + path;
    }
}

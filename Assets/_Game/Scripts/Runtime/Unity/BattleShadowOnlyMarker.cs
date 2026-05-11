using UnityEngine;
using UnityEngine.Rendering;

namespace SM.Unity;

/// <summary>
/// Foreground 트리/오브젝트에 붙이면 mesh는 invisible하지만 그림자는 정상 cast.
/// 사용법: tree prefab을 Scene view에 drop → 이 컴포넌트 attach → 자동으로 ShadowsOnly 적용.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BattleShadowOnlyMarker : MonoBehaviour
{
    [Tooltip("Children renderer도 같이 ShadowsOnly로 만들기")]
    [SerializeField] private bool includeChildren = true;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        var renderers = includeChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();

        foreach (var renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            renderer.receiveShadows = false;
        }
    }
}

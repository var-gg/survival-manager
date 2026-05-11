using UnityEngine;
using UnityEngine.Rendering;

namespace SM.Unity;

/// <summary>
/// Foreground 트리/오브젝트에 붙이면 메쉬는 안 보이지만 그림자는 정상 cast.
/// 사용법: tree prefab을 Scene view에 drop → 이 컴포넌트 attach → 자동으로 ShadowsOnly 적용.
/// 시야 안 막고 그림자만 ground에 떨어뜨리고 싶을 때.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BattleShadowOnlyMarker : MonoBehaviour
{
    [Tooltip("자식 GameObject의 렌더러까지 같이 ShadowsOnly로 만들기 (보통 켜둠 — 트리 prefab은 자식들로 구성됨)")]
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

using UnityEngine;
using UnityEngine.Rendering;

namespace SM.Unity;

/// <summary>
/// GPT-Pro 권장 contact shadow blob — 캐릭터 발밑에 soft dark ellipse를 두어
/// 캐릭터가 지면에 떠 보이지 않게 anchor. Realtime shadow가 자주 흔들리는
/// stylized cel 환경에서 readability의 가장 큰 single lift.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleActorContactShadow : MonoBehaviour
{
    [SerializeField] private Transform? feetSocket;
    [SerializeField] private float radiusX = 0.55f;
    [SerializeField] private float radiusZ = 0.35f;
    [SerializeField] private float liftAboveGround = 0.02f;
    [SerializeField] private Color shadowColor = new(0.04f, 0.05f, 0.06f, 0.55f);
    [SerializeField] private string shadowName = "ContactShadow";

    private GameObject? _shadowGo;

    public void Apply(Transform? overrideFeet = null)
    {
        if (overrideFeet != null)
        {
            feetSocket = overrideFeet;
        }

        var anchor = feetSocket != null ? feetSocket : transform;

        if (_shadowGo == null)
        {
            _shadowGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _shadowGo.name = shadowName;

            // Remove collider — visual only.
            var collider = _shadowGo.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            _shadowGo.transform.SetParent(anchor, false);
        }
        else
        {
            _shadowGo.transform.SetParent(anchor, false);
        }

        _shadowGo.transform.localPosition = new Vector3(0f, liftAboveGround, 0f);
        _shadowGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        _shadowGo.transform.localScale = new Vector3(radiusX * 2f, radiusZ * 2f, 1f);

        var renderer = _shadowGo.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            // Unlit transparent with built-in soft circle texture (Particle default).
            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            var softCircleTex = CreateRadialCircleTexture();
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", softCircleTex);
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", softCircleTex);
            }
            if (material.HasProperty("_Color"))
            {
                material.color = shadowColor;
            }
            else if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", shadowColor);
            }
            material.renderQueue = (int)RenderQueue.Transparent - 1;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    private static Texture2D CreateRadialCircleTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "BattleContactShadowFallbackTex",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        var center = (size - 1) * 0.5f;
        var maxR = center;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var dist = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
                var alpha = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Clamp01(dist), 2.2f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private void Awake()
    {
        Apply();
    }

    private void OnDestroy()
    {
        if (_shadowGo == null)
        {
            return;
        }

        var rendererMaterial = _shadowGo.GetComponent<MeshRenderer>()?.sharedMaterial;
        if (rendererMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(rendererMaterial);
            }
            else
            {
                DestroyImmediate(rendererMaterial);
            }
        }
    }
}

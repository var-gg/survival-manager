using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

internal sealed class CompendiumPrefabVfxPreviewStage
{
    private const int PreviewLayer = 30;
    private const int TextureWidth = 512;
    private const int TextureHeight = 256;

    private GameObject? _root;
    private Camera? _camera;
    private RenderTexture? _texture;
    private GameObject? _instance;
    private GameObject? _currentPrefab;
    private int _lastPlayToken = -1;

    public void Render(Image image, GameObject? prefab, int playToken)
    {
        if (image == null)
        {
            return;
        }

        if (prefab == null || !Application.isPlaying)
        {
            Clear(image);
            return;
        }

        EnsureStage();
        if (_texture != null)
        {
            image.image = _texture;
            image.style.display = DisplayStyle.Flex;
        }

        if (_currentPrefab == prefab && _lastPlayToken == playToken)
        {
            return;
        }

        _currentPrefab = prefab;
        _lastPlayToken = playToken;
        Spawn(prefab);
    }

    public void Clear(Image image)
    {
        if (image != null)
        {
            image.image = null;
            image.style.display = DisplayStyle.None;
        }

        DestroyObject(_instance);
        _instance = null;
        _currentPrefab = null;
        _lastPlayToken = -1;
    }

    private void EnsureStage()
    {
        if (_root != null && _camera != null && _texture != null)
        {
            return;
        }

        _texture = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = "SM_CompendiumVfxPreviewTexture",
        };
        _texture.Create();

        _root = new GameObject("SM_CompendiumVfxPreviewStage")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        _root.transform.position = new Vector3(0f, -5000f, 0f);
        _root.layer = PreviewLayer;

        var cameraObject = new GameObject("Camera")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        cameraObject.transform.SetParent(_root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.35f, -4.2f);
        cameraObject.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        cameraObject.layer = PreviewLayer;
        _camera = cameraObject.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _camera.orthographic = true;
        _camera.orthographicSize = 1.45f;
        _camera.cullingMask = 1 << PreviewLayer;
        _camera.targetTexture = _texture;

        var lightObject = new GameObject("Light")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        lightObject.transform.SetParent(_root.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.8f, -1.6f);
        lightObject.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
        lightObject.layer = PreviewLayer;
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.cullingMask = 1 << PreviewLayer;
    }

    private void Spawn(GameObject prefab)
    {
        DestroyObject(_instance);
        if (_root == null)
        {
            return;
        }

        _instance = Object.Instantiate(prefab, _root.transform);
        _instance.name = $"{prefab.name}_CompendiumPreview";
        _instance.transform.localPosition = Vector3.zero;
        _instance.transform.localRotation = Quaternion.identity;
        _instance.transform.localScale = Vector3.one;
        SetLayerRecursively(_instance, PreviewLayer);
        FrameInstance(_instance);
        foreach (var particleSystem in _instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private void FrameInstance(GameObject instance)
    {
        if (_camera == null)
        {
            return;
        }

        if (!TryCalculateBounds(instance, out var bounds))
        {
            _camera.orthographicSize = 1.45f;
            _camera.transform.localPosition = new Vector3(0f, 0.35f, -4.2f);
            return;
        }

        instance.transform.localPosition -= bounds.center - (_root != null ? _root.transform.position : Vector3.zero);
        var size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 1f);
        _camera.orthographicSize = Mathf.Clamp(size * 0.72f, 0.9f, 3.1f);
        _camera.transform.localPosition = new Vector3(0f, Mathf.Clamp(size * 0.18f, 0.25f, 1.2f), -Mathf.Clamp(size * 2.2f, 3.6f, 8f));
    }

    private static bool TryCalculateBounds(GameObject instance, out Bounds bounds)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(instance.transform.position, Vector3.one);
        var hasBounds = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void DestroyObject(Object? target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}

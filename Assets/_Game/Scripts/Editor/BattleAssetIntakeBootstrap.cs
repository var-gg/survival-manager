using System.IO;
using SM.Editor.Validation;
using SM.Combat.Model;
using SM.Unity;
using SM.Unity.Sandbox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SM.Editor.Bootstrap;

public static class BattleAssetIntakeBootstrap
{
    private const string PrefabFolder = "Assets/_Game/Prefabs/Battle/Actors";
    private const string TemplateFolder = "Assets/_Game/Prefabs/Battle/Actors/Templates";
    private const string PrimitivePrefabPath = "Assets/_Game/Prefabs/Battle/Actors/BattleActor_PrimitiveWrapper.prefab";
    private const string VendorTemplatePrefabPath = "Assets/_Game/Prefabs/Battle/Actors/Templates/BattleActor_VendorWrapperTemplate.prefab";
    private const string CatalogFolder = "Assets/Resources/_Game/Battle";
    private const string CatalogPath = "Assets/Resources/_Game/Battle/BattleActorPresentationCatalog.asset";
    private const string ScenePath = "Assets/_Game/Scenes/BattleAssetIntakeSandbox.unity";
    private const string AutoBootstrapSessionKey = "SM.BattleAssetIntakeBootstrap.Ensured";

    [InitializeOnLoadMethod]
    private static void AutoEnsureOnLoad()
    {
        EditorApplication.delayCall -= TryAutoEnsure;
        EditorApplication.delayCall += TryAutoEnsure;
    }

    [MenuItem("SM/Setup/Repair Battle Asset Intake Assets")]
    public static void RepairBattleAssetIntakeAssets()
    {
        EnsureFolder("Assets/_Game/Prefabs");
        EnsureFolder("Assets/_Game/Prefabs/Battle");
        EnsureFolder(PrefabFolder);
        EnsureFolder(TemplateFolder);
        EnsureFolder("Assets/Resources/_Game");
        EnsureFolder(CatalogFolder);

        var primitivePrefab = EnsureWrapperPrefab(PrimitivePrefabPath, includePrimitiveVisuals: true);
        var vendorTemplatePrefab = EnsureWrapperPrefab(VendorTemplatePrefabPath, includePrimitiveVisuals: false);
        EnsureCatalogAsset(primitivePrefab);
        EnsureSandboxScene(primitivePrefab, vendorTemplatePrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    public static void EnsureBattleAssetIntakeAssets()
    {
        RepairBattleAssetIntakeAssets();
    }

    private static void TryAutoEnsure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (SessionState.GetBool(AutoBootstrapSessionKey, false))
        {
            return;
        }

        if (File.Exists(PrimitivePrefabPath)
            && File.Exists(VendorTemplatePrefabPath)
            && File.Exists(CatalogPath)
            && File.Exists(ScenePath))
        {
            SessionState.SetBool(AutoBootstrapSessionKey, true);
            return;
        }

        try
        {
            RepairBattleAssetIntakeAssets();
            SessionState.SetBool(AutoBootstrapSessionKey, true);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BattleAssetIntakeBootstrap] Auto ensure failed: {ex.Message}");
        }
    }

    private static BattleActorWrapper EnsureWrapperPrefab(string path, bool includePrimitiveVisuals)
    {
        var root = BuildWrapperRoot(
            includePrimitiveVisuals
                ? "BattleActor_PrimitiveWrapper"
                : "BattleActor_VendorWrapperTemplate",
            includePrimitiveVisuals);

        try
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            var wrapper = prefab.GetComponent<BattleActorWrapper>();
            var report = BattleActorWrapperValidator.Validate(wrapper, path);
            if (report.HasErrors)
            {
                throw new System.InvalidOperationException(report.BuildSummary());
            }

            return wrapper;
        }
        finally
        {
            Object.DestroyImmediate(root.gameObject);
        }
    }

    private static BattleActorWrapper BuildWrapperRoot(string rootName, bool includePrimitiveVisuals)
    {
        var root = new GameObject(rootName);
        var wrapper = root.AddComponent<BattleActorWrapper>();
        root.AddComponent<BattleActorView>();
        var adapter = root.AddComponent<BattlePrimitiveActorVisualAdapter>();
        root.AddComponent<BattleAnimationEventBridge>();
        root.AddComponent<BattleActorVfxSurface>();
        root.AddComponent<BattleActorAudioSurface>();

        var socketRig = CreateChild(root.transform, "SocketRig");
        var center = CreateChild(socketRig, "Center");
        center.localPosition = new Vector3(0f, 0.10f, 0f);
        var head = CreateChild(socketRig, "Head");
        head.localPosition = new Vector3(0f, 2.0f, 0f);
        var hud = CreateChild(socketRig, "Hud");
        hud.localPosition = new Vector3(0f, 2.25f, 0f);
        var hit = CreateChild(socketRig, "Hit");
        hit.localPosition = new Vector3(0f, 1.05f, 0.28f);
        var feet = CreateChild(socketRig, "Feet");
        feet.localPosition = new Vector3(0f, -0.98f, 0f);
        var telegraph = CreateChild(socketRig, "Telegraph");
        telegraph.localPosition = feet.localPosition;
        var cameraFocus = CreateChild(socketRig, "CameraFocus");
        cameraFocus.localPosition = new Vector3(0f, 1.2f, 0f);

        var visualRoot = CreateChild(root.transform, "VisualRoot");
        var vendorVisualSlot = CreateChild(visualRoot, "VendorVisualSlot");
        var cast = CreateChild(visualRoot, "Cast");
        cast.localPosition = new Vector3(0f, 0.22f, 0.72f);
        var projectileOrigin = CreateChild(visualRoot, "ProjectileOrigin");
        projectileOrigin.localPosition = cast.localPosition;

        Renderer? bodyRenderer = null;
        Renderer? shadowRenderer = null;
        if (includePrimitiveVisuals)
        {
            shadowRenderer = CreatePrimitiveRenderer(
                visualRoot,
                PrimitiveType.Cylinder,
                "GroundShadow",
                new Vector3(0f, -1.02f, 0f),
                new Vector3(0.58f, 0.03f, 0.58f),
                new Color(0f, 0f, 0f, 0.28f));
            bodyRenderer = CreatePrimitiveRenderer(
                visualRoot,
                PrimitiveType.Capsule,
                "Body",
                Vector3.zero,
                new Vector3(0.92f, 1.10f, 0.92f),
                new Color(0.28f, 0.58f, 1f, 1f));
        }

        wrapper.ConfigureAuthoring(
            visualRoot,
            vendorVisualSlot,
            center,
            head,
            hud,
            hit,
            feet,
            telegraph,
            cast,
            projectileOrigin,
            cameraFocus);
        adapter.ConfigureAuthoring(visualRoot, bodyRenderer, shadowRenderer, includePrimitiveVisuals);
        return wrapper;
    }

    private static void EnsureCatalogAsset(BattleActorWrapper primitivePrefab)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<BattleActorPresentationCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BattleActorPresentationCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.SetDefaultWrapper(primitivePrefab);
        catalog.SetTeamDefaultWrapper(TeamSide.Ally, primitivePrefab);
        catalog.SetTeamDefaultWrapper(TeamSide.Enemy, primitivePrefab);
        EditorUtility.SetDirty(catalog);
    }

    private static void EnsureSandboxScene(BattleActorWrapper primitivePrefab, BattleActorWrapper vendorTemplatePrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        cameraGo.transform.position = new Vector3(0f, 8f, -8f);
        cameraGo.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

        UiEventSystemConfigurator.EnsureSceneEventSystem(scene);

        var root = new GameObject("BattleAssetIntakeSandboxRoot");
        var presentationRoot = new GameObject("BattlePresentationRoot");
        presentationRoot.transform.SetParent(root.transform, false);
        var stageRoot = new GameObject("BattleStageRoot");
        stageRoot.transform.SetParent(root.transform, false);
        var cameraRoot = new GameObject("BattleCameraRoot");
        cameraRoot.transform.SetParent(root.transform, false);
        cameraRoot.AddComponent<BattleCameraController>();

        var presentation = presentationRoot.AddComponent<BattlePresentationController>();

        var overlayCanvasGo = new GameObject("ActorOverlayCanvas", typeof(RectTransform));
        overlayCanvasGo.transform.SetParent(root.transform, false);
        var overlayCanvas = overlayCanvasGo.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 0;
        overlayCanvasGo.AddComponent<CanvasScaler>();
        var overlayRootGo = new GameObject("ActorOverlayRoot", typeof(RectTransform), typeof(Image));
        overlayRootGo.transform.SetParent(overlayCanvasGo.transform, false);
        var overlayRoot = overlayRootGo.GetComponent<RectTransform>();
        overlayRoot.anchorMin = Vector2.zero;
        overlayRoot.anchorMax = Vector2.one;
        overlayRoot.offsetMin = Vector2.zero;
        overlayRoot.offsetMax = Vector2.zero;
        var overlayImage = overlayRootGo.GetComponent<Image>();
        overlayImage.color = Color.clear;
        overlayImage.raycastTarget = false;

        var sandbox = root.AddComponent<BattleAssetIntakeSandboxController>();

        Bind(presentation, "battleStageRoot", stageRoot.transform);
        Bind(presentation, "actorOverlayRoot", overlayRoot);
        Bind(sandbox, "presentationController", presentation);
        Bind(sandbox, "primitiveWrapperPrefab", primitivePrefab);
        Bind(sandbox, "vendorTemplateWrapperPrefab", vendorTemplatePrefab);

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        var folderName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Renderer CreatePrimitiveRenderer(
        Transform parent,
        PrimitiveType primitiveType,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Color color)
    {
        var go = GameObject.CreatePrimitive(primitiveType);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateEditorMaterial(color);
        return renderer;
    }

    private static void Bind(Object target, string fieldName, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            throw new System.InvalidOperationException($"Missing serialized field '{fieldName}' on {target.GetType().Name}.");
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static Material CreateEditorMaterial(Color color)
    {
        var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader);
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }
}

using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public sealed class BattlePrimitiveActorVisualAdapter : BattleActorVisualAdapter
{
    private const float GroundShadowWorldY = -0.965f;
    private static readonly Vector3 GroundShadowWorldScale = new(0.58f, 0.006f, 0.58f);

    [SerializeField] private Transform visualRoot = null!;
    [SerializeField] private Renderer bodyRenderer = null!;
    [SerializeField] private Renderer shadowRenderer = null!;
    [SerializeField] private bool createRuntimePrimitivesIfMissing = true;

    public override Transform? VisualRoot => visualRoot;
    public override Renderer? PrimaryRenderer => bodyRenderer;
    public override Renderer? ShadowRenderer => shadowRenderer;

    public void ConfigureAuthoring(
        Transform visualRootTransform,
        Renderer? body,
        Renderer? shadow,
        bool allowRuntimePrimitiveCreation)
    {
        visualRoot = visualRootTransform;
        bodyRenderer = body!;
        shadowRenderer = shadow!;
        createRuntimePrimitivesIfMissing = allowRuntimePrimitiveCreation;
    }

    public override void Initialize(BattleActorWrapper wrapper, BattleUnitReadModel actor)
    {
        if (visualRoot == null)
        {
            visualRoot = wrapper.VisualRoot != wrapper.transform
                ? wrapper.VisualRoot
                : CreateChild(wrapper.transform, "VisualRoot");
        }

        if (shadowRenderer == null && createRuntimePrimitivesIfMissing)
        {
            shadowRenderer = CreatePrimitiveRenderer(
                visualRoot,
                PrimitiveType.Cylinder,
                "GroundShadow",
                Vector3.zero,
                GroundShadowWorldScale,
                new Color(0f, 0f, 0f, 0.28f));
        }

        if (bodyRenderer == null && createRuntimePrimitivesIfMissing)
        {
            var bodyScale = actor.Anchor.IsFrontRow()
                ? new Vector3(0.96f, 1.18f, 0.96f)
                : new Vector3(0.90f, 1.02f, 0.90f);
            bodyRenderer = CreatePrimitiveRenderer(
                visualRoot,
                PrimitiveType.Capsule,
                "Body",
                Vector3.zero,
                bodyScale,
                Color.white);
        }

        EnsureMaterial(bodyRenderer, Color.white);
        EnsureMaterial(shadowRenderer, new Color(0f, 0f, 0f, 0.28f));
    }

    public override void ApplyState(in BattleActorVisualState state)
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = state.LocalPosition;
            visualRoot.localScale = state.LocalScale;
            visualRoot.localRotation = state.LocalRotation;
        }

        if (bodyRenderer != null)
        {
            BattlePresentationMaterialFactory.ApplyColor(bodyRenderer.sharedMaterial, state.BodyColor);
        }

        if (shadowRenderer != null)
        {
            ApplyGroundShadow(state.ShadowColor);
        }
    }

    private void ApplyGroundShadow(Color shadowColor)
    {
        if (shadowRenderer == null)
        {
            return;
        }

        BattlePresentationMaterialFactory.ApplyColor(shadowRenderer.sharedMaterial, shadowColor);
        var shadow = shadowRenderer.transform;
        shadow.SetPositionAndRotation(new Vector3(transform.position.x, GroundShadowWorldY, transform.position.z), Quaternion.identity);
        shadow.localScale = DivideByParentScale(shadow, GroundShadowWorldScale);
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
            DestroyPresentationObject(collider);
        }

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = BattlePresentationMaterialFactory.Create(color);
        return renderer;
    }

    private static Vector3 DivideByParentScale(Transform child, Vector3 worldScale)
    {
        var parentScale = child.parent != null ? child.parent.lossyScale : Vector3.one;
        return new Vector3(
            worldScale.x / SafeScale(parentScale.x),
            worldScale.y / SafeScale(parentScale.y),
            worldScale.z / SafeScale(parentScale.z));
    }

    private static float SafeScale(float scale)
    {
        var abs = Mathf.Abs(scale);
        return abs <= 0.0001f ? 1f : abs;
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void EnsureMaterial(Renderer? renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        if (renderer.sharedMaterial == null)
        {
            renderer.sharedMaterial = BattlePresentationMaterialFactory.Create(color);
        }
    }

    private static void DestroyPresentationObject(Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

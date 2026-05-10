using System;
using SM.Unity.UI.Atlas;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SM.Unity.Atlas;

[DisallowMultipleComponent]
public sealed class Atlas3DSceneController : MonoBehaviour
{
    [SerializeField] private Camera worldCamera = null!;
    [SerializeField] private AtlasScreenController screenController = null!;
    [SerializeField] private AtlasSigilAuraVFXController auraController = null!;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (screenController != null)
        {
            screenController.ViewStateRendered += HandleViewStateRendered;
        }
    }

    private void OnDisable()
    {
        if (screenController != null)
        {
            screenController.ViewStateRendered -= HandleViewStateRendered;
        }
    }

    private void Start()
    {
        ResolveReferences();
        screenController?.EnsureRuntimeControls();
        if (screenController?.CurrentState != null)
        {
            HandleViewStateRendered(screenController.CurrentState);
        }
    }

    private void Update()
    {
        if (!TryReadPointerDown(out var screenPosition) || worldCamera == null || screenController == null)
        {
            return;
        }

        var ray = worldCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out var hit, 200f))
        {
            return;
        }

        if (TryResolveNodeId(hit.collider, out var nodeId))
        {
            screenController.SelectTileFromWorld(nodeId);
        }
    }

    private void HandleViewStateRendered(AtlasScreenViewState state)
    {
        if (auraController != null)
        {
            auraController.Render(state);
        }
    }

    private void ResolveReferences()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (screenController == null)
        {
            screenController = FindFirstObjectByType<AtlasScreenController>();
        }

        if (auraController == null)
        {
            auraController = GetComponentInChildren<AtlasSigilAuraVFXController>();
            if (auraController == null)
            {
                auraController = gameObject.AddComponent<AtlasSigilAuraVFXController>();
            }
        }
    }

    private static bool TryResolveNodeId(Collider collider, out string nodeId)
    {
        for (var current = collider.transform; current != null; current = current.parent)
        {
            if (current.name.StartsWith("HexHit_", StringComparison.Ordinal))
            {
                nodeId = current.name["HexHit_".Length..];
                return !string.IsNullOrWhiteSpace(nodeId);
            }
        }

        nodeId = string.Empty;
        return false;
    }

    private static bool TryReadPointerDown(out Vector2 screenPosition)
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            return true;
        }

        screenPosition = default;
        return false;
    }
}

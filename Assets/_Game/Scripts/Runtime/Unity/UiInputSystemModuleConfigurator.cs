using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SM.Unity;

public static class UiInputSystemModuleConfigurator
{
    private const string UiActionMapName = "UI";
    private const string NavigateActionName = "Navigate";
    private const string SubmitActionName = "Submit";
    private const string CancelActionName = "Cancel";
    private const string PointActionName = "Point";
    private const string ClickActionName = "Click";
    private const string RightClickActionName = "RightClick";
    private const string MiddleClickActionName = "MiddleClick";
    private const string ScrollWheelActionName = "ScrollWheel";
    private const string TrackedDevicePositionActionName = "TrackedDevicePosition";
    private const string TrackedDeviceOrientationActionName = "TrackedDeviceOrientation";

    public static bool TryConfigure(InputSystemUIInputModule module, out string error)
    {
        if (module == null)
        {
            error = "InputSystemUIInputModule reference is null.";
            return false;
        }

        var asset = module.actionsAsset;
        if (asset == null)
        {
            module.AssignDefaultActions();
            asset = module.actionsAsset;
        }

        if (asset == null)
        {
            error = "No InputActionAsset is assigned to the UI input module.";
            return false;
        }

        return TryConfigure(module, asset, out error);
    }

    public static bool TryConfigure(InputSystemUIInputModule module, InputActionAsset asset, out string error)
    {
        if (module == null)
        {
            error = "InputSystemUIInputModule reference is null.";
            return false;
        }

        if (asset == null)
        {
            error = "InputActionAsset reference is null.";
            return false;
        }

        var actionMap = asset.FindActionMap(UiActionMapName, throwIfNotFound: false);
        if (actionMap == null)
        {
            error = $"InputActionAsset '{asset.name}' is missing the '{UiActionMapName}' action map.";
            return false;
        }

        var navigate = actionMap.FindAction(NavigateActionName, throwIfNotFound: false);
        var submit = actionMap.FindAction(SubmitActionName, throwIfNotFound: false);
        var cancel = actionMap.FindAction(CancelActionName, throwIfNotFound: false);
        var point = actionMap.FindAction(PointActionName, throwIfNotFound: false);
        var click = actionMap.FindAction(ClickActionName, throwIfNotFound: false);

        if (navigate == null || submit == null || cancel == null || point == null || click == null)
        {
            error = $"InputActionAsset '{asset.name}' does not provide the canonical UI actions.";
            return false;
        }

        module.actionsAsset = asset;
        module.move = CreateReference(module.move, navigate);
        module.submit = CreateReference(module.submit, submit);
        module.cancel = CreateReference(module.cancel, cancel);
        module.point = CreateReference(module.point, point);
        module.leftClick = CreateReference(module.leftClick, click);
        module.rightClick = CreateOptionalReference(module.rightClick, actionMap, RightClickActionName);
        module.middleClick = CreateOptionalReference(module.middleClick, actionMap, MiddleClickActionName);
        module.scrollWheel = CreateOptionalReference(module.scrollWheel, actionMap, ScrollWheelActionName);
        module.trackedDevicePosition = CreateOptionalReference(module.trackedDevicePosition, actionMap, TrackedDevicePositionActionName);
        module.trackedDeviceOrientation = CreateOptionalReference(module.trackedDeviceOrientation, actionMap, TrackedDeviceOrientationActionName);

        error = string.Empty;
        return true;
    }

    private static InputActionReference CreateReference(InputActionReference? currentReference, InputAction action)
    {
        if (currentReference != null && currentReference.action == action)
        {
            return currentReference;
        }

        return InputActionReference.Create(action);
    }

    private static InputActionReference? CreateOptionalReference(InputActionReference? currentReference, InputActionMap actionMap, string actionName)
    {
        var action = actionMap.FindAction(actionName, throwIfNotFound: false);
        return action != null ? CreateReference(currentReference, action) : null;
    }
}

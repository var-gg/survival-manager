using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SM.Unity;

public sealed class BattleCameraController : MonoBehaviour
{
    private const float PanSpeedKeyboard = 12f;
    private const float EdgeScrollSpeed = 10f;
    private const float EdgeScrollMargin = 20f;
    private const float SmoothTime = 0.08f;
    private const float ZoomSmoothTime = 0.12f;
    private const float ZoomStep = 1.5f;
    private const float MinZoomHeight = 4f;
    private const float MaxZoomHeight = 12f;

    // Ground-plane bounds for the camera look-at point
    private const float GroundBoundsMinX = -8f;
    private const float GroundBoundsMaxX = 8f;
    private const float GroundBoundsMinZ = -4f;
    private const float GroundBoundsMaxZ = 4f;

    private Camera _camera = null!;
    private Vector3 _defaultPosition;
    private Quaternion _fixedRotation;
    private Vector3 _targetPosition;
    private float _targetZoomHeight;
    private Vector3 _velocity;
    private float _zoomVelocity;
    private Vector3 _dragOrigin;
    private bool _isDragging;
    private bool _inputEnabled;

    // Camera-to-ground offset for proper bounds clamping
    private Vector3 _cameraToGroundOffset;
    private Func<bool>? _uiBlockPredicate;

    // --- New Input System actions (created programmatically) ---
    private InputAction _pointerPositionAction = null!;
    private InputAction _rightClickAction = null!;
    private InputAction _middleClickAction = null!;
    private InputAction _scrollAction = null!;
    private InputAction _movePanAction = null!;

    public Camera Camera => _camera;
    public bool IsDragging => _isDragging;

    public void Initialize(Vector3 defaultPosition, Quaternion fixedRotation)
    {
        _camera = Camera.main!;
        _defaultPosition = defaultPosition;
        _fixedRotation = fixedRotation;
        _targetPosition = defaultPosition;
        _targetZoomHeight = defaultPosition.y;
        _velocity = Vector3.zero;
        _zoomVelocity = 0f;
        _isDragging = false;
        _inputEnabled = true;

        _cameraToGroundOffset = ComputeCameraToGroundOffset(defaultPosition, fixedRotation);

        if (_camera != null)
        {
            _camera.transform.position = defaultPosition;
            _camera.transform.rotation = fixedRotation;
        }

        CreateInputActions();
        EnableInputActions(true);
    }

    public void ResetToDefault()
    {
        _targetPosition = _defaultPosition;
        _targetZoomHeight = _defaultPosition.y;
        _velocity = Vector3.zero;
        _zoomVelocity = 0f;
        _isDragging = false;
        _cameraToGroundOffset = ComputeCameraToGroundOffset(_defaultPosition, _fixedRotation);
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled)
        {
            _isDragging = false;
        }

        EnableInputActions(enabled);
    }

    public void SetUiBlockPredicate(Func<bool>? uiBlockPredicate)
    {
        _uiBlockPredicate = uiBlockPredicate;
    }

    private void OnDestroy()
    {
        DisposeInputActions();
    }

    private void CreateInputActions()
    {
        _pointerPositionAction = new InputAction("PointerPosition", InputActionType.Value, "<Mouse>/position");
        _rightClickAction = new InputAction("RightClick", InputActionType.Button, "<Mouse>/rightButton");
        _middleClickAction = new InputAction("MiddleClick", InputActionType.Button, "<Mouse>/middleButton");
        _scrollAction = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");

        // WASD + Arrow keys composite for camera panning
        _movePanAction = new InputAction("MovePan", InputActionType.Value);
        _movePanAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        _movePanAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
    }

    private void EnableInputActions(bool enable)
    {
        if (_pointerPositionAction == null) return;

        if (enable)
        {
            _pointerPositionAction.Enable();
            _rightClickAction.Enable();
            _middleClickAction.Enable();
            _scrollAction.Enable();
            _movePanAction.Enable();
        }
        else
        {
            _pointerPositionAction.Disable();
            _rightClickAction.Disable();
            _middleClickAction.Disable();
            _scrollAction.Disable();
            _movePanAction.Disable();
        }
    }

    private void DisposeInputActions()
    {
        _pointerPositionAction?.Dispose();
        _rightClickAction?.Dispose();
        _middleClickAction?.Dispose();
        _scrollAction?.Dispose();
        _movePanAction?.Dispose();
    }

    private void LateUpdate()
    {
        if (_camera == null || !_inputEnabled) return;

        HandleMouseDrag();
        HandleMouseZoom();
        HandleKeyboardPan();
        HandleEdgeScroll();

        ApplySmoothedMovement();
    }

    private void HandleMouseDrag()
    {
        var rightPressed = _rightClickAction.WasPressedThisFrame();
        var middlePressed = _middleClickAction.WasPressedThisFrame();
        var rightHeld = _rightClickAction.IsPressed();
        var middleHeld = _middleClickAction.IsPressed();
        var rightReleased = _rightClickAction.WasReleasedThisFrame();
        var middleReleased = _middleClickAction.WasReleasedThisFrame();

        if ((rightPressed || middlePressed) && !_isDragging)
        {
            if (IsPointerBlockedByUi())
            {
                return;
            }

            var mousePos = _pointerPositionAction.ReadValue<Vector2>();
            var groundPoint = ScreenToGroundPlane(mousePos);
            if (groundPoint.HasValue)
            {
                // Snap camera to target to prevent SmoothDamp oscillation during drag
                SnapCameraToTarget();
                _dragOrigin = groundPoint.Value;
                _isDragging = true;
            }
        }

        if (_isDragging && (rightHeld || middleHeld))
        {
            var mousePos = _pointerPositionAction.ReadValue<Vector2>();
            var currentPoint = ScreenToGroundPlane(mousePos);
            if (currentPoint.HasValue)
            {
                var delta = _dragOrigin - currentPoint.Value;
                _targetPosition += delta;
                ClampTargetPosition();
                // Snap immediately during drag for responsive feel
                SnapCameraToTarget();
            }
        }

        if ((rightReleased || middleReleased) && _isDragging)
        {
            if (!rightHeld && !middleHeld)
            {
                _isDragging = false;
            }
        }
    }

    private bool IsPointerBlockedByUi()
    {
        if (_uiBlockPredicate != null)
        {
            return _uiBlockPredicate.Invoke();
        }

        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void HandleMouseZoom()
    {
        var scrollDelta = _scrollAction.ReadValue<float>();
        if (Mathf.Abs(scrollDelta) < 0.01f) return;

        // Normalize scroll delta (New Input System reports larger values)
        var normalizedScroll = Mathf.Sign(scrollDelta);

        var prevHeight = _targetZoomHeight;
        _targetZoomHeight -= normalizedScroll * ZoomStep;
        _targetZoomHeight = Mathf.Clamp(_targetZoomHeight, MinZoomHeight, MaxZoomHeight);

        if (Mathf.Abs(_targetZoomHeight - prevHeight) < 0.001f) return;

        // Snap camera to prevent SmoothDamp oscillation during zoom
        SnapCameraToTarget();

        // Zoom toward cursor: shift XZ target slightly toward mouse world position
        var mousePos = _pointerPositionAction.ReadValue<Vector2>();
        var cursorGround = ScreenToGroundPlane(mousePos);
        if (cursorGround.HasValue)
        {
            var directionToCursor = cursorGround.Value - _targetPosition;
            directionToCursor.y = 0f;
            var zoomInfluence = normalizedScroll > 0 ? 0.1f : -0.05f;
            _targetPosition += directionToCursor * zoomInfluence;

            // Recompute ground offset after zoom height change
            var futurePos = new Vector3(_targetPosition.x, _targetZoomHeight, _targetPosition.z);
            _cameraToGroundOffset = ComputeCameraToGroundOffset(futurePos, _fixedRotation);
            ClampTargetPosition();
            SnapCameraToTarget();
        }
    }

    private void HandleKeyboardPan()
    {
        var moveInput = _movePanAction.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < 0.01f) return;

        // Transform movement direction relative to camera facing (projected on XZ)
        var camForward = _camera.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();
        var camRight = _camera.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        var worldDir = camForward * moveInput.y + camRight * moveInput.x;
        _targetPosition += worldDir.normalized * (PanSpeedKeyboard * Time.deltaTime);
        ClampTargetPosition();
    }

    private void HandleEdgeScroll()
    {
        if (_isDragging) return;
        if (!Application.isFocused) return;

        var mousePos = _pointerPositionAction.ReadValue<Vector2>();
        var moveDir = Vector3.zero;

        if (mousePos.x <= EdgeScrollMargin)
            moveDir.x -= 1f;
        else if (mousePos.x >= Screen.width - EdgeScrollMargin)
            moveDir.x += 1f;

        if (mousePos.y <= EdgeScrollMargin)
            moveDir.z -= 1f;
        else if (mousePos.y >= Screen.height - EdgeScrollMargin)
            moveDir.z += 1f;

        if (moveDir.sqrMagnitude > 0.01f)
        {
            var camForward = _camera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            var camRight = _camera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            var worldDir = camForward * moveDir.z + camRight * moveDir.x;
            _targetPosition += worldDir.normalized * (EdgeScrollSpeed * Time.deltaTime);
            ClampTargetPosition();
        }
    }

    private void ApplySmoothedMovement()
    {
        var currentPos = _camera.transform.position;
        var desiredPos = new Vector3(_targetPosition.x, _targetZoomHeight, _targetPosition.z);
        var smoothed = Vector3.SmoothDamp(currentPos, desiredPos, ref _velocity, SmoothTime);
        _camera.transform.position = smoothed;
        _camera.transform.rotation = _fixedRotation;
    }

    private void SnapCameraToTarget()
    {
        _camera.transform.position = new Vector3(_targetPosition.x, _targetZoomHeight, _targetPosition.z);
        _velocity = Vector3.zero;
        _zoomVelocity = 0f;
    }

    private void ClampTargetPosition()
    {
        // Clamp the ground look-at point (not the camera position) to battlefield bounds
        var groundX = _targetPosition.x + _cameraToGroundOffset.x;
        var groundZ = _targetPosition.z + _cameraToGroundOffset.z;
        groundX = Mathf.Clamp(groundX, GroundBoundsMinX, GroundBoundsMaxX);
        groundZ = Mathf.Clamp(groundZ, GroundBoundsMinZ, GroundBoundsMaxZ);
        _targetPosition.x = groundX - _cameraToGroundOffset.x;
        _targetPosition.z = groundZ - _cameraToGroundOffset.z;
    }

    private Vector3? ScreenToGroundPlane(Vector2 screenPoint)
    {
        var ray = _camera.ScreenPointToRay(new Vector3(screenPoint.x, screenPoint.y, 0f));
        var groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out var distance))
        {
            return ray.GetPoint(distance);
        }

        return null;
    }

    /// <summary>
    /// Computes the XZ offset from camera position to where its forward ray hits the Y=0 ground plane.
    /// Used to clamp the look-at point (not the camera) within battlefield bounds.
    /// </summary>
    private static Vector3 ComputeCameraToGroundOffset(Vector3 cameraPos, Quaternion rotation)
    {
        var forward = rotation * Vector3.forward;
        if (Mathf.Abs(forward.y) < 0.001f) return Vector3.zero;
        var t = -cameraPos.y / forward.y;
        var groundHit = cameraPos + t * forward;
        return new Vector3(groundHit.x - cameraPos.x, 0f, groundHit.z - cameraPos.z);
    }
}

using UnityEngine;

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

    private static readonly Vector2 PanBoundsX = new(-8f, 8f);
    private static readonly Vector2 PanBoundsZ = new(-4f, 4f);

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

        if (_camera != null)
        {
            _camera.transform.position = defaultPosition;
            _camera.transform.rotation = fixedRotation;
        }
    }

    public void ResetToDefault()
    {
        _targetPosition = _defaultPosition;
        _targetZoomHeight = _defaultPosition.y;
        _velocity = Vector3.zero;
        _zoomVelocity = 0f;
        _isDragging = false;
    }

    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled)
        {
            _isDragging = false;
        }
    }

    private void LateUpdate()
    {
        if (_camera == null || !_inputEnabled) return;

#if ENABLE_LEGACY_INPUT_MANAGER
        HandleMouseDrag();
        HandleMouseZoom();
        HandleKeyboardPan();
        HandleEdgeScroll();
#endif

        ApplySmoothedMovement();
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    private void HandleMouseDrag()
    {
        var rightDown = Input.GetMouseButtonDown(1);
        var middleDown = Input.GetMouseButtonDown(2);
        var rightHeld = Input.GetMouseButton(1);
        var middleHeld = Input.GetMouseButton(2);
        var rightUp = Input.GetMouseButtonUp(1);
        var middleUp = Input.GetMouseButtonUp(2);

        if ((rightDown || middleDown) && !_isDragging)
        {
            var groundPoint = ScreenToGroundPlane(Input.mousePosition);
            if (groundPoint.HasValue)
            {
                _dragOrigin = groundPoint.Value;
                _isDragging = true;
            }
        }

        if (_isDragging && (rightHeld || middleHeld))
        {
            var currentPoint = ScreenToGroundPlane(Input.mousePosition);
            if (currentPoint.HasValue)
            {
                var delta = _dragOrigin - currentPoint.Value;
                _targetPosition += delta;
                ClampTargetPosition();
            }
        }

        if ((rightUp || middleUp) && _isDragging)
        {
            if (!rightHeld && !middleHeld)
            {
                _isDragging = false;
            }
        }
    }

    private void HandleMouseZoom()
    {
        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) < 0.01f) return;

        _targetZoomHeight -= scrollDelta * ZoomStep;
        _targetZoomHeight = Mathf.Clamp(_targetZoomHeight, MinZoomHeight, MaxZoomHeight);

        // Zoom toward cursor: shift XZ target slightly toward mouse world position
        var cursorGround = ScreenToGroundPlane(Input.mousePosition);
        if (cursorGround.HasValue)
        {
            var directionToCursor = cursorGround.Value - _targetPosition;
            directionToCursor.y = 0f;
            var zoomInfluence = scrollDelta > 0 ? 0.1f : -0.05f;
            _targetPosition += directionToCursor * zoomInfluence;
            ClampTargetPosition();
        }
    }

    private void HandleKeyboardPan()
    {
        var moveDir = Vector3.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            moveDir.z += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            moveDir.z -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            moveDir.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            moveDir.x += 1f;

        if (moveDir.sqrMagnitude > 0.01f)
        {
            // Transform movement direction relative to camera facing (projected on XZ)
            var camForward = _camera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            var camRight = _camera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            var worldDir = camForward * moveDir.z + camRight * moveDir.x;
            _targetPosition += worldDir.normalized * (PanSpeedKeyboard * Time.deltaTime);
            ClampTargetPosition();
        }
    }

    private void HandleEdgeScroll()
    {
        if (_isDragging) return;
        if (!Application.isFocused) return;

        var mousePos = Input.mousePosition;
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
#endif

    private void ApplySmoothedMovement()
    {
        var currentPos = _camera.transform.position;
        var desiredPos = new Vector3(_targetPosition.x, _targetZoomHeight, _targetPosition.z);
        var smoothed = Vector3.SmoothDamp(currentPos, desiredPos, ref _velocity, SmoothTime);
        _camera.transform.position = smoothed;
        _camera.transform.rotation = _fixedRotation;
    }

    private void ClampTargetPosition()
    {
        _targetPosition.x = Mathf.Clamp(_targetPosition.x, PanBoundsX.x, PanBoundsX.y);
        _targetPosition.z = Mathf.Clamp(_targetPosition.z, PanBoundsZ.x, PanBoundsZ.y);
    }

    private Vector3? ScreenToGroundPlane(Vector3 screenPoint)
    {
        var ray = _camera.ScreenPointToRay(screenPoint);
        var groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out var distance))
        {
            return ray.GetPoint(distance);
        }

        return null;
    }
}

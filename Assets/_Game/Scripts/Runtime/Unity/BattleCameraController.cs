using UnityEngine;

namespace SM.Unity;

public sealed class BattleCameraController : MonoBehaviour
{
    private const float PanSpeedKeyboard = 12f;
    private const float EdgeScrollSpeed = 10f;
    private const float EdgeScrollMargin = 20f;
    private const float SmoothTime = 0.08f;
    private const float ZoomStep = 1.5f;
    private const float MinZoomHeight = 4f;
    private const float MaxZoomHeight = 12f;

    // Ground-plane bounds: battlefield X=[-4.9, 4.9], Z=[-1.8, 1.8] + margin
    private const float GroundBoundsMinX = -8f;
    private const float GroundBoundsMaxX = 8f;
    private const float GroundBoundsMinZ = -5f;
    private const float GroundBoundsMaxZ = 5f;

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

    /// <summary>
    /// XZ offset from camera position to the ground point it looks at.
    /// Constant for a fixed rotation. Computed once during Initialize.
    /// </summary>
    private Vector3 _cameraToGroundOffset;

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

        HandleMouseDrag();
        HandleMouseZoom();
        HandleKeyboardPan();
        HandleEdgeScroll();

        ApplySmoothedMovement();
    }

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
                // Snap camera to target so ray math stays consistent
                SnapCameraToTarget();
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
                // Apply immediately so next frame's ray is from the correct position
                SnapCameraToTarget();
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

        // Zoom toward cursor
        var cursorGround = ScreenToGroundPlane(Input.mousePosition);
        if (cursorGround.HasValue)
        {
            var directionToCursor = cursorGround.Value - _targetPosition;
            directionToCursor.y = 0f;
            var zoomInfluence = scrollDelta > 0 ? 0.1f : -0.05f;
            _targetPosition += directionToCursor * zoomInfluence;
            ClampTargetPosition();
        }

        // Snap so next scroll frame's ray is from the updated height
        SnapCameraToTarget();
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

    private void ApplySmoothedMovement()
    {
        if (_isDragging)
        {
            // During drag, camera is already snapped — skip smoothing
            return;
        }

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
    }

    /// <summary>
    /// Clamp so that the ground point the camera looks at stays within battlefield bounds.
    /// </summary>
    private void ClampTargetPosition()
    {
        // Where the camera would look at on the ground with the current _targetPosition
        var groundX = _targetPosition.x + _cameraToGroundOffset.x;
        var groundZ = _targetPosition.z + _cameraToGroundOffset.z;

        // Clamp the ground look-at point to battlefield bounds
        groundX = Mathf.Clamp(groundX, GroundBoundsMinX, GroundBoundsMaxX);
        groundZ = Mathf.Clamp(groundZ, GroundBoundsMinZ, GroundBoundsMaxZ);

        // Translate back to camera position
        _targetPosition.x = groundX - _cameraToGroundOffset.x;
        _targetPosition.z = groundZ - _cameraToGroundOffset.z;
    }

    /// <summary>
    /// Compute the XZ offset from a camera position to where it hits the Y=0 ground plane.
    /// For a fixed rotation this is constant regardless of camera position.
    /// </summary>
    private static Vector3 ComputeCameraToGroundOffset(Vector3 cameraPos, Quaternion rotation)
    {
        var forward = rotation * Vector3.forward;
        if (Mathf.Abs(forward.y) < 0.001f)
        {
            return Vector3.zero;
        }

        // Ray from camera along forward direction: P = cameraPos + t * forward
        // Solve for Y=0: cameraPos.y + t * forward.y = 0
        var t = -cameraPos.y / forward.y;
        var groundHit = cameraPos + t * forward;
        return new Vector3(groundHit.x - cameraPos.x, 0f, groundHit.z - cameraPos.z);
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

using UnityEngine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Component responsible for crouch input handling.
/// Sets animator "IsSit" parameter and adjusts CharacterController based on Left Ctrl key state.
/// Prevents standing up when standing on Tunnel layer.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerCrouch : MonoBehaviour
{
    private Animator _animator;

    [Header("Crouch Settings")]
    [SerializeField] private float _crouchHeight = 1.2f;
    [SerializeField] private float _crouchRadius = 0.4f;

    [Header("Stand Check")]
    [Tooltip("Layer that prevents standing up when player is on it")]
    [SerializeField] private LayerMask _tunnelLayer;
    [Tooltip("Distance to check for ground below player")]
    [SerializeField] private float _groundCheckDistance = 0.3f;

    [Header("Camera")]
    private Transform _cameraTransform;
    [Tooltip("Target position for camera when crouching")]
    [SerializeField] private Transform _crouchCameraTarget;

    [Header("Gizmos")]
    [SerializeField] private Color _crouchGizmoColor = new Color(0f, 1f, 0f, 0.5f);

    private CharacterController _controller;
    private float _standHeight;
    private float _standRadius;
    private Vector3 _standCameraLocalPosition;
    private bool _isCrouching;
    private bool _wantsToCrouch;

    private readonly int _isSitHash = Animator.StringToHash("IsSit");

    /// <summary>
    /// Returns true if the player is currently crouching.
    /// </summary>
    public bool IsCrouching => _isCrouching;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (_animator == null)
            _animator = GetComponent<Animator>();

        // Save standing parameters
        if (_controller != null)
        {
            _standHeight = _controller.height;
            _standRadius = _controller.radius;
            Debug.Log($"[PlayerCrouch] Saved stand params: height={_standHeight}, radius={_standRadius}");
        }
        else
        {
            Debug.LogError("[PlayerCrouch] CharacterController not found!");
        }

        // Save standing camera local position
        if (_cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
                _cameraTransform = cam.transform;
        }

        if (_cameraTransform != null)
        {
            _standCameraLocalPosition = _cameraTransform.localPosition;
            Debug.Log($"[PlayerCrouch] Saved camera position: {_standCameraLocalPosition}");
        }
    }

    private void Update()
    {
        if (_controller == null) return;

        _wantsToCrouch = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        // Determine actual crouch state
        bool shouldCrouch;

        if (_wantsToCrouch)
        {
            // Player wants to crouch - always allow
            shouldCrouch = true;
        }
        else if (_isCrouching)
        {
            // Player wants to stand up - check if allowed
            shouldCrouch = !CanStandUp();
        }
        else
        {
            shouldCrouch = false;
        }

        if (shouldCrouch != _isCrouching)
        {
            _isCrouching = shouldCrouch;
            ApplyCrouchState(_isCrouching);
            Debug.Log($"[PlayerCrouch] Crouch state changed: {_isCrouching}, height={_controller.height}");
        }
    }

    /// <summary>
    /// Checks if player can stand up.
    /// Returns false if standing on Tunnel layer.
    /// </summary>
    private bool CanStandUp()
    {
        if (_controller == null) return true;

        // Raycast down to check what layer we're standing on
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, _groundCheckDistance, _tunnelLayer, QueryTriggerInteraction.Ignore))
        {
            // Standing on tunnel layer - cannot stand up
            Debug.Log($"[PlayerCrouch] Cannot stand up - on tunnel: {hit.collider.name}");
            return false;
        }

        return true;
    }

    private void ApplyCrouchState(bool crouching)
    {
        // Update animator
        if (_animator != null)
            _animator.SetBool(_isSitHash, crouching);

        // Update CharacterController
        if (_controller != null)
        {
            if (crouching)
            {
                _controller.height = _crouchHeight;
                _controller.center = new Vector3(0f, _crouchHeight / 2f, 0f);
                _controller.radius = _crouchRadius;
            }
            else
            {
                _controller.height = _standHeight;
                _controller.center = new Vector3(0f, _standHeight / 2f, 0f);
                _controller.radius = _standRadius;
            }
        }

        // Update camera position
        if (_cameraTransform != null)
        {
            if (crouching && _crouchCameraTarget != null)
            {
                _cameraTransform.localPosition = _crouchCameraTarget.localPosition;
            }
            else if (!crouching)
            {
                _cameraTransform.localPosition = _standCameraLocalPosition;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw crouch CharacterController preview
        Gizmos.color = _crouchGizmoColor;

        Vector3 scale = transform.lossyScale;
        float scaledRadius = _crouchRadius * Mathf.Max(scale.x, scale.z);
        float scaledHeight = _crouchHeight * scale.y;

        Vector3 worldCenter = transform.TransformPoint(new Vector3(0f, _crouchHeight / 2f, 0f));

        float bodyHeight = Mathf.Max(0f, scaledHeight - scaledRadius * 2f);
        float halfBodyHeight = bodyHeight / 2f;

        Vector3 topSphere = worldCenter + Vector3.up * halfBodyHeight;
        Vector3 bottomSphere = worldCenter - Vector3.up * halfBodyHeight;

        Gizmos.DrawWireSphere(topSphere, scaledRadius);
        Gizmos.DrawWireSphere(bottomSphere, scaledRadius);

        Gizmos.DrawLine(topSphere + Vector3.forward * scaledRadius, bottomSphere + Vector3.forward * scaledRadius);
        Gizmos.DrawLine(topSphere - Vector3.forward * scaledRadius, bottomSphere - Vector3.forward * scaledRadius);
        Gizmos.DrawLine(topSphere + Vector3.right * scaledRadius, bottomSphere + Vector3.right * scaledRadius);
        Gizmos.DrawLine(topSphere - Vector3.right * scaledRadius, bottomSphere - Vector3.right * scaledRadius);

        // Draw ground check ray
        Gizmos.color = Color.yellow;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * _groundCheckDistance);
    }
}

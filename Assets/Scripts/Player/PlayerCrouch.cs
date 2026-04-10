using UnityEngine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Component responsible for crouch input handling.
/// Sets animator "IsSit" parameter and adjusts CharacterController based on Left Ctrl key state.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerCrouch : MonoBehaviour
{
    private Animator _animator;

    [Header("Crouch Settings")]
    [SerializeField] private float _crouchHeight = 1.2f;
    [SerializeField] private float _crouchRadius = 0.4f;
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

    private readonly int _isSitHash = Animator.StringToHash("IsSit");

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
        if (_cameraTransform = GetComponentInChildren<Camera>().transform)
        {
            _standCameraLocalPosition = _cameraTransform.localPosition;
            Debug.Log($"[PlayerCrouch] Saved camera position: {_standCameraLocalPosition}");
        }
    }

    private void Update()
    {
        if (_controller == null) return;

        bool crouchPressed = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        if (crouchPressed != _isCrouching)
        {
            _isCrouching = crouchPressed;
            ApplyCrouchState(_isCrouching);
            Debug.Log($"[PlayerCrouch] Crouch state changed: {_isCrouching}, height={_controller.height}");
        }
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
                // Move camera to crouch target position (use local position relative to player)
                _cameraTransform.localPosition = _crouchCameraTarget.localPosition;
            }
            else
            {
                // Return camera to standing position
                _cameraTransform.localPosition = _standCameraLocalPosition;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw crouch CharacterController preview matching exactly how Unity draws it
        Gizmos.color = _crouchGizmoColor;

        // Get current scale
        Vector3 scale = transform.lossyScale;
        float scaledRadius = _crouchRadius * Mathf.Max(scale.x, scale.z);
        float scaledHeight = _crouchHeight * scale.y;

        // CharacterController center is in local space, convert to world
        Vector3 worldCenter = transform.TransformPoint(new Vector3(0f, _crouchHeight / 2f, 0f));

        // Calculate capsule body (the cylindrical part between two hemispheres)
        // Height includes the two hemispheres, so body height = height - 2 * radius
        float bodyHeight = Mathf.Max(0f, scaledHeight - scaledRadius * 2f);
        float halfBodyHeight = bodyHeight / 2f;

        // Sphere centers
        Vector3 topSphere = worldCenter + Vector3.up * halfBodyHeight;
        Vector3 bottomSphere = worldCenter - Vector3.up * halfBodyHeight;

        // Draw the two hemispheres
        Gizmos.DrawWireSphere(topSphere, scaledRadius);
        Gizmos.DrawWireSphere(bottomSphere, scaledRadius);

        // Draw vertical lines connecting the spheres
        Gizmos.DrawLine(topSphere + Vector3.forward * scaledRadius, bottomSphere + Vector3.forward * scaledRadius);
        Gizmos.DrawLine(topSphere - Vector3.forward * scaledRadius, bottomSphere - Vector3.forward * scaledRadius);
        Gizmos.DrawLine(topSphere + Vector3.right * scaledRadius, bottomSphere + Vector3.right * scaledRadius);
        Gizmos.DrawLine(topSphere - Vector3.right * scaledRadius, bottomSphere - Vector3.right * scaledRadius);
    }
}

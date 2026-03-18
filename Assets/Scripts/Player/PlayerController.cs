using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Networked first-person controller. Owner-authoritative movement via NetworkTransform (Owner mode).
/// Only the owner processes input and moves; remote players see synced transform.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;
    [SerializeField] private float _sprintSpeed = 7f;
    [SerializeField] private float _crouchSpeed = 2f;
    [SerializeField] private float _gravity = -15f;

    [Header("Look")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _lookSensitivity = 0.15f;
    [SerializeField] private float _verticalLookLimit = 85f;

    [Header("Crouch")]
    [SerializeField] private float _standHeight = 2f;
    [SerializeField] private float _crouchHeight = 1.2f;
    [SerializeField] private float _crouchTransitionSpeed = 8f;

    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Step Events")]
    [SerializeField] private float _stepInterval = 0.5f;
    [SerializeField] private float _sprintStepInterval = 0.35f;

    private CharacterController _controller;
    private float _verticalVelocity;
    private float _cameraPitch;
    private float _targetHeight;
    private float _stepTimer;
    private bool _isSprinting;
    private bool _isCrouching;
    private Camera _camera;

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _sprintAction;
    private InputAction _crouchAction;

    /// <summary>
    /// Fired every footstep on the owner client. Args: position, isSprinting.
    /// </summary>
    public event System.Action<Vector3, bool> OnFootstep;

    public bool IsGrounded { get; private set; }
    public bool IsSprinting => _isSprinting;
    public bool IsCrouching => _isCrouching;
    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        // Ensure owner-authoritative transform sync
        var nt = GetComponent<NetworkTransform>();
        if (nt != null)
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

        // Disable CharacterController until OnNetworkSpawn so NGO can place us at the correct spawn position
        _controller.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        _targetHeight = _standHeight;

        // Server consumes the spawn point and sends it to the owning client via RPC
        if (IsServer && GameNetworkManager.ConsumeSpawnPoint(OwnerClientId, out var spawnPos, out var spawnRot))
        {
            // Teleport server-side instance immediately (works for host player who is both server+owner)
            _controller.enabled = false;
            transform.SetPositionAndRotation(spawnPos, spawnRot);
            _controller.enabled = true;
            Debug.Log($"[Player] Server set spawn: {spawnPos} for client {OwnerClientId}");

            // For remote clients, send RPC so the owner (authority) applies position on their side
            if (!IsOwner)
            {
                TeleportOwnerClientRpc(spawnPos, spawnRot.eulerAngles.y,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } });
            }
        }

        // Re-enable CharacterController
        if (!_controller.enabled)
            _controller.enabled = true;

        Debug.Log($"[Player] OnNetworkSpawn: IsOwner={IsOwner}, position={transform.position}");

        if (_inputActions != null)
        {
            var map = _inputActions.FindActionMap("Player");
            _moveAction = map?.FindAction("Move");
            _lookAction = map?.FindAction("Look");
            _sprintAction = map?.FindAction("Sprint");
            _crouchAction = map?.FindAction("Crouch");
        }

        if (IsOwner)
        {
            _moveAction?.Enable();
            _lookAction?.Enable();
            _sprintAction?.Enable();
            _crouchAction?.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Enable only the owner's camera
            _camera = _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;
            if (_camera != null)
                _camera.enabled = true;

            // Enable AudioListener only on owner
            var listener = _cameraTransform != null ? _cameraTransform.GetComponent<AudioListener>() : null;
            if (listener != null)
                listener.enabled = true;
        }
        else
        {
            // Disable camera and audio listener for remote players
            _camera = _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;
            if (_camera != null)
                _camera.enabled = false;

            var listener = _cameraTransform != null ? _cameraTransform.GetComponent<AudioListener>() : null;
            if (listener != null)
                listener.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            _moveAction?.Disable();
            _lookAction?.Disable();
            _sprintAction?.Disable();
            _crouchAction?.Disable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned) return;

        UpdateGroundCheck();
        UpdateLook();
        UpdateMovement();
        UpdateCrouch();
    }

    private void UpdateGroundCheck()
    {
        IsGrounded = _controller.isGrounded;
        if (IsGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
    }

    private void UpdateLook()
    {
        Vector2 lookInput = _lookAction != null ? _lookAction.ReadValue<Vector2>() : default;

        float yaw = lookInput.x * _lookSensitivity;
        float pitch = lookInput.y * _lookSensitivity;

        _cameraPitch -= pitch;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -_verticalLookLimit, _verticalLookLimit);

        transform.Rotate(Vector3.up, yaw);

        if (_cameraTransform != null)
            _cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    private void UpdateMovement()
    {
        Vector2 moveInput = _moveAction != null ? _moveAction.ReadValue<Vector2>() : default;
        float sprintValue = _sprintAction != null ? _sprintAction.ReadValue<float>() : 0f;
        _isSprinting = sprintValue > 0.5f && !_isCrouching;

        float speed = _isCrouching ? _crouchSpeed : (_isSprinting ? _sprintSpeed : _walkSpeed);
        CurrentSpeed = moveInput.magnitude * speed;

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        _controller.Move(move * (speed * Time.deltaTime));

        _verticalVelocity += _gravity * Time.deltaTime;
        _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));

        UpdateStepTimer(moveInput);
    }

    private void UpdateCrouch()
    {
        float crouchValue = _crouchAction != null ? _crouchAction.ReadValue<float>() : 0f;
        bool crouchPressed = crouchValue > 0.5f;
        _isCrouching = crouchPressed;
        _targetHeight = _isCrouching ? _crouchHeight : _standHeight;

        float currentHeight = _controller.height;
        if (!Mathf.Approximately(currentHeight, _targetHeight))
        {
            float newHeight = Mathf.MoveTowards(currentHeight, _targetHeight, _crouchTransitionSpeed * Time.deltaTime);
            float heightDelta = newHeight - currentHeight;
            _controller.height = newHeight;
            _controller.center = new Vector3(0f, newHeight / 2f, 0f);
            transform.position += Vector3.up * (heightDelta / 2f);

            if (_cameraTransform != null)
                _cameraTransform.localPosition = new Vector3(0f, newHeight - 0.1f, 0f);
        }
    }

    private void UpdateStepTimer(Vector2 moveInput)
    {
        if (!IsGrounded || moveInput.sqrMagnitude < 0.01f)
        {
            _stepTimer = 0f;
            return;
        }

        float interval = _isSprinting ? _sprintStepInterval : _stepInterval;
        _stepTimer += Time.deltaTime;

        if (_stepTimer >= interval)
        {
            _stepTimer -= interval;
            OnFootstep?.Invoke(transform.position, _isSprinting);
        }
    }

    [ClientRpc]
    private void TeleportOwnerClientRpc(Vector3 position, float yaw, ClientRpcParams rpcParams = default)
    {
        _controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        _controller.enabled = true;
        Debug.Log($"[Player] Owner teleported to {position}");
    }

}

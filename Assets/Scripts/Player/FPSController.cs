using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person controller: CharacterController movement, mouse look, walk/sprint/crouch.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;
    [SerializeField] private float _sprintSpeed = 7f;
    [SerializeField] private float _crouchSpeed = 2f;
    [SerializeField] private float _gravity = -15f;
    [SerializeField] private float _groundCheckDistance = 0.2f;

    [Header("Look")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _lookSensitivity = 0.15f;
    [SerializeField] private float _verticalLookLimit = 85f;

    [Header("Crouch")]
    [SerializeField] private float _standHeight = 2f;
    [SerializeField] private float _crouchHeight = 1.2f;
    [SerializeField] private float _crouchTransitionSpeed = 8f;

    [Header("Input")]
    [SerializeField] private InputActionReference _moveAction;
    [SerializeField] private InputActionReference _lookAction;
    [SerializeField] private InputActionReference _sprintAction;
    [SerializeField] private InputActionReference _crouchAction;

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

    /// <summary>
    /// Fired every footstep. Args: position, isSprinting.
    /// PlayerEchoLocator will subscribe to this in Phase 2.
    /// </summary>
    public event System.Action<Vector3, bool> OnFootstep;

    public bool IsGrounded { get; private set; }
    public bool IsSprinting => _isSprinting;
    public bool IsCrouching => _isCrouching;
    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _targetHeight = _standHeight;
    }

    private void OnEnable()
    {
        EnableAction(_moveAction);
        EnableAction(_lookAction);
        EnableAction(_sprintAction);
        EnableAction(_crouchAction);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        DisableAction(_moveAction);
        DisableAction(_lookAction);
        DisableAction(_sprintAction);
        DisableAction(_crouchAction);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
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
        Vector2 lookInput = ReadAction<Vector2>(_lookAction);

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
        Vector2 moveInput = ReadAction<Vector2>(_moveAction);
        _isSprinting = ReadAction<float>(_sprintAction) > 0.5f && !_isCrouching;

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
        bool crouchPressed = ReadAction<float>(_crouchAction) > 0.5f;
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

    private static void EnableAction(InputActionReference actionRef)
    {
        if (actionRef != null && actionRef.action != null)
            actionRef.action.Enable();
    }

    private static void DisableAction(InputActionReference actionRef)
    {
        if (actionRef != null && actionRef.action != null)
            actionRef.action.Disable();
    }

    private static T ReadAction<T>(InputActionReference actionRef) where T : struct
    {
        if (actionRef != null && actionRef.action != null)
            return actionRef.action.ReadValue<T>();
        return default;
    }
}

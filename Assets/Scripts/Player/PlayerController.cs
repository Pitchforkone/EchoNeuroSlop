using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;


[RequireComponent(typeof(CharacterController))]
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

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float _animSmoothTime = 0.1f;
    [SerializeField] private NetworkAnimator _networkAnimator;

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
    private AudioListener _audioListener;
    private bool _isSetup;
    private Vector2 _smoothAnimInput;

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _sprintAction;
    private InputAction _crouchAction;

    /// <summary>
    /// Fired every footstep. Args: position, isSprinting.
    /// </summary>
    public event System.Action<Vector3, bool> OnFootstep;

    public bool IsGrounded { get; private set; }
    public bool IsSprinting => _isSprinting;
    public bool IsCrouching => _isCrouching;
    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _networkAnimator = GetComponent<NetworkAnimator>();
        _targetHeight = _standHeight;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetupLocalPlayer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Для удалённых игроков отключаем CharacterController,
        // чтобы NetworkTransform мог устанавливать позицию
        if (!isLocalPlayer)
        {
            if (_controller != null)
                _controller.enabled = false;
        }
    }

    private void Start()
    {
        // Для одиночной игры - без OnStartLocalPlayer
        // Для мультиплеера (если без NetworkClient) - тоже инициализируем
        if (!NetworkClient.active)
        {
            SetupLocalPlayer();
        }
        else if (!isLocalPlayer)
        {
            // Для удалённых игроков отключаем камеру и аудио
            DisableRemotePlayerComponents();
        }
    }

    private void SetupLocalPlayer()
    {
        if (_isSetup) return;
        _isSetup = true;

        if (_inputActions != null)
        {
            var map = _inputActions.FindActionMap("Player");
            _moveAction = map?.FindAction("Move");
            _lookAction = map?.FindAction("Look");
            _sprintAction = map?.FindAction("Sprint");
            _crouchAction = map?.FindAction("Crouch");
        }

        _moveAction?.Enable();
        _lookAction?.Enable();
        _sprintAction?.Enable();
        _crouchAction?.Enable();

        // Lock cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Enable camera
        _camera = _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;
        if (_camera != null)
            _camera.enabled = true;

        // Enable AudioListener
        _audioListener = _cameraTransform != null ? _cameraTransform.GetComponent<AudioListener>() : null;
        if (_audioListener != null)
            _audioListener.enabled = true;
    }

    private void DisableRemotePlayerComponents()
    {
        // Для удалённых игроков отключаем камеру и аудио
        _camera = _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;
        if (_camera != null)
            _camera.enabled = false;

        _audioListener = _cameraTransform != null ? _cameraTransform.GetComponent<AudioListener>() : null;
        if (_audioListener != null)
            _audioListener.enabled = false;
    }

    private void OnDestroy()
    {
        _moveAction?.Disable();
        _lookAction?.Disable();
        _sprintAction?.Disable();
        _crouchAction?.Disable();

        if (_isSetup)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        // Обновление логики игрока производится только на локальном клиенте
        if (!_isSetup) return;
        if (NetworkClient.active && !isLocalPlayer) return;

        UpdateGroundCheck();
        UpdateLook();
        UpdateMovement();
        // UpdateCrouch(); // Crouch handling moved to PlayerCrouch component
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

        UpdateAnimator(moveInput);
        UpdateStepTimer(moveInput);
    }

    private void UpdateAnimator(Vector2 moveInput)
    {
        if (_animator == null) return;

        _smoothAnimInput = Vector2.MoveTowards(_smoothAnimInput, moveInput, Time.deltaTime / _animSmoothTime);
        
        // Устанавливаем параметры напрямую - NetworkAnimator синхронизирует их
        _animator.SetFloat("X", _smoothAnimInput.x);
        _animator.SetFloat("Y", _smoothAnimInput.y);
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
}

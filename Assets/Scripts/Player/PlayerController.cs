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

    [Header("Look")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _lookSensitivity = 0.15f;
    [SerializeField] private float _verticalLookLimit = 85f;

    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float _animSmoothTime = 0.1f;

    private CharacterController _controller;
    private float _cameraPitch;
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

    public bool IsSprinting => _isSprinting;
    public bool IsCrouching => _isCrouching;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetupLocalPlayer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (!isLocalPlayer)
        {
            if (_controller != null)
                _controller.enabled = false;
        }
    }

    private void Start()
    {
        if (!NetworkClient.active)
        {
            SetupLocalPlayer();
        }
        else if (!isLocalPlayer)
        {
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
        if (!_isSetup) return;
        if (NetworkClient.active && !isLocalPlayer) return;

        UpdateLook();
        UpdateMovement();
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

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        _controller.Move(move * (speed * Time.deltaTime));

        UpdateAnimator(moveInput);
    }

    private void UpdateAnimator(Vector2 moveInput)
    {
        if (_animator == null) return;

        _smoothAnimInput = Vector2.MoveTowards(_smoothAnimInput, moveInput, Time.deltaTime / _animSmoothTime);
        
        // Устанавливаем параметры напрямую - NetworkAnimator синхронизирует их
        _animator.SetFloat("X", _smoothAnimInput.x);
        _animator.SetFloat("Y", _smoothAnimInput.y);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using System;


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

    private InputSystem_Actions _inputSystemActions;

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

    private Vector2 _moveVector;
    private Vector2 _lookVector;

    public Action<WalkType> OnWalkTypeChanged;
    public WalkType walkType = WalkType.Walk;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _inputSystemActions = new InputSystem_Actions();
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

        _inputSystemActions.Enable();
        _inputSystemActions.Player.Crouch.started += ctx => _isCrouching = true;
        _inputSystemActions.Player.Crouch.canceled += ctx => _isCrouching = false;
        _inputSystemActions.Player.Sprint.started += ctx => _isSprinting = true;
        _inputSystemActions.Player.Sprint.canceled += ctx => _isSprinting = false;
        _inputSystemActions.Player.Move.performed += ctx => _moveVector = ctx.ReadValue<Vector2>();
        _inputSystemActions.Player.Move.canceled += ctx => _moveVector = Vector2.zero;
        _inputSystemActions.Player.Look.performed += ctx => _lookVector = ctx.ReadValue<Vector2>();
        _inputSystemActions.Player.Look.canceled += ctx => _lookVector = Vector2.zero;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _camera = _cameraTransform != null ? _cameraTransform.GetComponent<Camera>() : null;
        if (_camera != null)
            _camera.enabled = true;

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

    private void Update()
    {
        if (!_isSetup) return;
        if (NetworkClient.active && !isLocalPlayer) return;

        UpdateLook();
        UpdateMovement();
        UpdateWalkState();
    }
    private void UpdateWalkState()
    {
        if (!_isSetup) return;
        var bufwalkType = walkType;
        if(_isCrouching)
        {
            walkType = WalkType.Crouch;
        }
        if(_isSprinting)
        {
            walkType = WalkType.Sprint;
        }
        if(!_isCrouching && !_isSprinting)
        {
            walkType = WalkType.Walk;
        }
        if(bufwalkType != walkType)
        {
            OnWalkTypeChanged?.Invoke(walkType);
        }
    }
    private void UpdateLook()
    { 
        float yaw = _lookVector.x * _lookSensitivity;
        float pitch = _lookVector.y * _lookSensitivity;

        _cameraPitch -= pitch;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -_verticalLookLimit, _verticalLookLimit);

        transform.Rotate(Vector3.up, yaw);

        if (_cameraTransform != null)
            _cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    private void UpdateMovement()
    {
        float speed = _isCrouching ? _crouchSpeed : (_isSprinting ? _sprintSpeed : _walkSpeed);

        Vector3 move = transform.right * _moveVector.x + transform.forward * _moveVector.y;
        _controller.Move(move * (speed * Time.deltaTime));

        UpdateAnimator(_moveVector);
    }

    private void UpdateAnimator(Vector2 moveInput)
    {
        if (_animator == null) return;
        _animator.SetFloat("X", moveInput.x);
        _animator.SetFloat("Y", moveInput.y);
    }
}
public enum  WalkType
{
    Walk,
    Sprint,
    Crouch
}
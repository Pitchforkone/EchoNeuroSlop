using UnityEngine;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimatorController : MonoBehaviour
{
    private Animator _animator;
    private PlayerController _playerController;

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

    private CharacterController _controller;
    private float _standHeight;
    private float _standRadius;
    private Vector3 _standCameraLocalPosition;
    private bool _isCrouching;
    private bool _isRunning;

    private readonly int _isSitHash = Animator.StringToHash("IsSit");
    private readonly int _isRunHash = Animator.StringToHash("IsRun");
    public bool IsCrouching => _isCrouching;
    public bool IsRunning => _isRunning;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (TryGetComponent<PlayerController>(out _playerController))
        {
            if (!_playerController.isLocalPlayer)
                _playerController.OnWalkTypeChanged += UpdateState;
        }
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_controller != null)
        {
            _standHeight = _controller.height;
            _standRadius = _controller.radius;
        }

        if (_cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
                _cameraTransform = cam.transform;
        }

        if (_cameraTransform != null)
        {
            _standCameraLocalPosition = _cameraTransform.localPosition;
        }
    }
    public void UpdateState(WalkType previousWalkType, WalkType currentWalkType)
    {
        // Update crouch state
        bool shouldCrouch = currentWalkType == WalkType.Crouch;

        // If we want to stop crouching, check if we can stand up
        if (!shouldCrouch && _isCrouching)
        {
            shouldCrouch = !CanStandUp();
        }

        if (shouldCrouch != _isCrouching)
        {
            _isCrouching = shouldCrouch;
            ApplyCrouchState(_isCrouching);
        }

        // Update sprint state
        bool shouldRun = currentWalkType == WalkType.Sprint;
        if (shouldRun != _isRunning)
        {
            _isRunning = shouldRun;
            if (_animator != null)
                _animator.SetBool(_isRunHash, _isRunning);
        }
    }
    private void Update()
    {
        if (_controller == null) return;

        // Только локальный игрок должен обрабатывать ввод
        if (_playerController != null && !_playerController.isLocalPlayer)
            return;

        // Check if we're crouching and blocked from standing up
        if (_isCrouching && !CanStandUp())
        {
            // Keep crouching if we can't stand up
            return;
        }
    }

    private bool CanStandUp()
    {
        if (_controller == null) return true;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, _groundCheckDistance, _tunnelLayer, QueryTriggerInteraction.Ignore))
        {
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
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player echo locator for single-player.
/// Active echo: LMB (Attack action) with cooldown.
/// Passive echo: footstep events from PlayerController.
/// </summary>
public class PlayerEchoLocator : MonoBehaviour
{
    [Header("Presets")]
    [SerializeField] private EchoPreset _activePingPreset;
    [SerializeField] private EchoPreset _footstepPreset;
    [SerializeField] private EchoPreset _sprintFootstepPreset;

    [Header("Active Echo")]
    [SerializeField] private float _activeCooldown = 2f;

    [Header("Passive Echo")]
    [SerializeField] private float _footstepCooldown = 0.3f;

    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    private PlayerController _playerController;
    private FPSController _fpsController;
    private InputAction _echoAction;
    private float _lastActiveTime = -999f;
    private float _lastFootstepTime = -999f;

    public float CooldownRemaining => Mathf.Max(0f, _activeCooldown - (Time.time - _lastActiveTime));
    public float CooldownNormalized => Mathf.Clamp01(CooldownRemaining / _activeCooldown);

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _fpsController = GetComponent<FPSController>();
    }

    private void OnEnable()
    {
        if (_inputActions != null)
        {
            _echoAction = _inputActions.FindActionMap("Player")?.FindAction("Attack");
            if (_echoAction != null)
            {
                _echoAction.Enable();
                _echoAction.performed += OnEchoPerformed;
            }
        }

        if (_playerController != null)
            _playerController.OnFootstep += OnFootstep;

        if (_fpsController != null)
            _fpsController.OnFootstep += OnFootstep;
    }

    private void OnDisable()
    {
        if (_echoAction != null)
        {
            _echoAction.performed -= OnEchoPerformed;
            _echoAction.Disable();
        }

        if (_playerController != null)
            _playerController.OnFootstep -= OnFootstep;

        if (_fpsController != null)
            _fpsController.OnFootstep -= OnFootstep;
    }

    private void OnEchoPerformed(InputAction.CallbackContext ctx)
    {
        if (Time.time - _lastActiveTime < _activeCooldown) return;
        if (_activePingPreset == null || EchoManager.Instance == null) return;

        _lastActiveTime = Time.time;
        EchoManager.Instance.SpawnEcho(transform.position, _activePingPreset);
    }

    private void OnFootstep(Vector3 position, bool isSprinting)
    {
        if (EchoManager.Instance == null) return;
        if (Time.time - _lastFootstepTime < _footstepCooldown) return;

        // Crouching = silent, no passive echo
        bool isCrouching = (_playerController != null && _playerController.IsCrouching) ||
                          (_fpsController != null && _fpsController.IsCrouching);
        if (isCrouching) return;

        var preset = isSprinting && _sprintFootstepPreset != null ? _sprintFootstepPreset : _footstepPreset;
        if (preset == null) return;

        _lastFootstepTime = Time.time;
        EchoManager.Instance.SpawnEcho(position, preset);
    }
}

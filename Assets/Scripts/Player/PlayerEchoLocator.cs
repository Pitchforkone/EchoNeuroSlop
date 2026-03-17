using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player echo locator: active echo on LMB (Attack action), passive echo from footsteps.
/// Requires FPSController on the same GameObject for footstep events.
/// </summary>
public class PlayerEchoLocator : MonoBehaviour
{
    [Header("Presets")]
    [SerializeField] private EchoPreset _activePingPreset;
    [SerializeField] private EchoPreset _footstepPreset;

    [Header("Active Echo")]
    [SerializeField] private float _activeCooldown = 2f;

    [Header("Input")]
    [SerializeField] private InputActionReference _echoAction; // Attack / LMB

    private FPSController _fpsController;
    private float _lastActiveTime = -999f;

    public float CooldownRemaining => Mathf.Max(0f, _activeCooldown - (Time.time - _lastActiveTime));
    public float CooldownNormalized => Mathf.Clamp01(CooldownRemaining / _activeCooldown);

    private void Awake()
    {
        _fpsController = GetComponent<FPSController>();
    }

    private void OnEnable()
    {
        if (_echoAction != null && _echoAction.action != null)
        {
            _echoAction.action.Enable();
            _echoAction.action.performed += OnEchoPerformed;
        }

        if (_fpsController != null)
            _fpsController.OnFootstep += OnFootstep;
    }

    private void OnDisable()
    {
        if (_echoAction != null && _echoAction.action != null)
        {
            _echoAction.action.performed -= OnEchoPerformed;
            _echoAction.action.Disable();
        }

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
        if (_footstepPreset == null || EchoManager.Instance == null) return;

        // Crouching = silent, no passive echo
        if (_fpsController != null && _fpsController.IsCrouching) return;

        EchoManager.Instance.SpawnEcho(position, _footstepPreset);
    }
}

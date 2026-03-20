using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Networked player echo locator. Only the owner fires echoes.
/// Active echo: LMB (Attack action) with cooldown.
/// Passive echo: footstep events from PlayerController.
/// All echoes go through EchoManager (ServerRpc → ClientRpc → all clients see them).
/// </summary>
public class PlayerEchoLocator : NetworkBehaviour
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
    private InputAction _echoAction;
    private float _lastActiveTime = -999f;
    private float _lastFootstepTime = -999f;

    public float CooldownRemaining => Mathf.Max(0f, _activeCooldown - (Time.time - _lastActiveTime));
    public float CooldownNormalized => Mathf.Clamp01(CooldownRemaining / _activeCooldown);

    public override void OnNetworkSpawn()
    {
        _playerController = GetComponent<PlayerController>();

        if (!IsOwner) return;

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
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (_echoAction != null)
        {
            _echoAction.performed -= OnEchoPerformed;
            _echoAction.Disable();
        }

        if (_playerController != null)
            _playerController.OnFootstep -= OnFootstep;
    }

    private void OnEchoPerformed(InputAction.CallbackContext ctx)
    {
        if (!IsOwner) return;
        if (Time.time - _lastActiveTime < _activeCooldown) return;
        if (_activePingPreset == null || EchoManager.Instance == null) return;

        _lastActiveTime = Time.time;
        EchoManager.Instance.SpawnEcho(transform.position, _activePingPreset);
    }

    private void OnFootstep(Vector3 position, bool isSprinting)
    {
        if (!IsOwner) return;
        if (EchoManager.Instance == null) return;
        if (Time.time - _lastFootstepTime < _footstepCooldown) return;

        // Crouching = silent, no passive echo
        if (_playerController != null && _playerController.IsCrouching) return;

        var preset = isSprinting && _sprintFootstepPreset != null ? _sprintFootstepPreset : _footstepPreset;
        if (preset == null) return;

        _lastFootstepTime = Time.time;
        EchoManager.Instance.SpawnEcho(position, preset);
    }
}

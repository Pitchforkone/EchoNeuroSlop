using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

/// <summary>
/// Player echo locator for multiplayer with Photon PUN 2.
/// Active echo: LMB (Attack action) with cooldown or loud sound into microphone.
/// Passive echo: footstep events from PlayerController.
/// </summary>
public class PlayerEchoLocator : MonoBehaviourPun
{
    [Header("Presets")]
    [SerializeField] private EchoPreset _activePingPreset;
    [SerializeField] private EchoPreset _footstepPreset;
    [SerializeField] private EchoPreset _sprintFootstepPreset;

    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Microphone Detection")]
    [SerializeField] private float _volumeThreshold = 0.01f;
    [SerializeField] private int _sampleWindow = 128;
    [SerializeField] private float _minVolumeChangeStep = 0.005f;
    [SerializeField] private float _echoIntervalWhenLoud = 0.5f;

    [Header("Active Echo Cooldown")]
    [SerializeField] private float _activeEchoCooldown = 0.5f;

    private PlayerController _playerController;
    private FPSController _fpsController;
    private InputAction _echoAction;
    private SharedMicrophone _microphone;

    private float _lastVolume;
    private float _lastEchoTime;
    private float _lastActiveEchoTime;
    private bool _isSetup;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _fpsController = GetComponent<FPSController>();
        _microphone = GetComponent<SharedMicrophone>();
    }

    private void OnEnable()
    {
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            SetupInput();
        }

        if (_playerController != null)
            _playerController.OnFootstep += OnFootstep;

        if (_fpsController != null)
            _fpsController.OnFootstep += OnFootstep;
    }

    private void SetupInput()
    {
        if (_isSetup) return;
        _isSetup = true;

        if (_inputActions != null)
        {
            _echoAction = _inputActions.FindActionMap("Player")?.FindAction("Attack");
            if (_echoAction != null)
            {
                _echoAction.Enable();
                _echoAction.performed += OnEchoPerformed;
            }
        }
    }

    private SharedMicrophone GetMicrophone()
    {
        if (_microphone != null && _microphone.IsRecording)
            return _microphone;

        return SharedMicrophone.LocalInstance;
    }

    private void Update()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TriggerActiveEchoWithCooldown();
        }

        var mic = GetMicrophone();
        if (mic == null || !mic.IsRecording || mic.Clip == null) return;

        float volume = GetMicrophoneVolume(mic);
        float volumeChange = Mathf.Abs(volume - _lastVolume);

        if (volumeChange >= _minVolumeChangeStep && volume > _volumeThreshold)
        {
            TriggerActiveEchoFromMicrophone();
            _lastVolume = volume;
            _lastEchoTime = Time.time;
        }
        else if (volume > _volumeThreshold && Time.time - _lastEchoTime >= _echoIntervalWhenLoud)
        {
            TriggerActiveEchoFromMicrophone();
            _lastEchoTime = Time.time;
        }
        else if (volume <= _volumeThreshold)
        {
            _lastVolume = volume;
        }
    }

    private float GetMicrophoneVolume(SharedMicrophone mic)
    {
        if (mic == null || mic.Clip == null) return 0f;

        int micPosition = mic.GetPosition() - _sampleWindow;
        if (micPosition < 0) return 0f;

        float[] samples = new float[_sampleWindow];
        mic.Clip.GetData(samples, micPosition);

        float sum = 0f;
        for (int i = 0; i < _sampleWindow; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }

        return sum / _sampleWindow;
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

        _isSetup = false;
    }

    private void OnEchoPerformed(InputAction.CallbackContext ctx)
    {
        TriggerActiveEchoWithCooldown();
    }

    private void TriggerActiveEchoWithCooldown()
    {
        if (Time.time - _lastActiveEchoTime < _activeEchoCooldown) return;

        TriggerActiveEcho();
        _lastActiveEchoTime = Time.time;
    }

    private void TriggerActiveEcho()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;
        if (_activePingPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(transform.position, _activePingPreset);
    }

    private void TriggerActiveEchoFromMicrophone()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;
        if (_activePingPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(transform.position, _activePingPreset);
    }

    private void OnFootstep(Vector3 position, bool isSprinting)
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;
        if (EchoManager.Instance == null) return;

        bool isCrouching = (_playerController != null && _playerController.IsCrouching) ||
                          (_fpsController != null && _fpsController.IsCrouching);
        if (isCrouching) return;

        var preset = isSprinting && _sprintFootstepPreset != null ? _sprintFootstepPreset : _footstepPreset;
        if (preset == null) return;

        EchoManager.Instance.SpawnEcho(position, preset);
    }
}

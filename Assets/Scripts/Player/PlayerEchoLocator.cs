using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// Player echo locator for multiplayer with Mirror.
/// Active echo: LMB (Attack action) with cooldown or loud sound into microphone.
/// Passive echo: footstep events from PlayerController.
/// </summary>
public class PlayerEchoLocator : NetworkBehaviour
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

    private PlayerController _playerController;
    private FPSController _fpsController;
    private InputAction _echoAction;

    private AudioClip _microphoneClip;
    private string _microphoneName;
    private bool _microphoneInitialized;
    private float _lastVolume;
    private float _lastEchoTime;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _fpsController = GetComponent<FPSController>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetupInput();
        SetupMicrophone();
    }
    private void OnEnable()
    {
        // Для синглплеера
        if (!NetworkClient.active)
        {
            SetupInput();
            SetupMicrophone();
        }

        if (_playerController != null)
            _playerController.OnFootstep += OnFootstep;

        if (_fpsController != null)
            _fpsController.OnFootstep += OnFootstep;
    }

    private void SetupInput()
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
    }

    private void SetupMicrophone()
    {
        if (_microphoneInitialized) return;

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone detected.");
            return;
        }

        _microphoneName = Microphone.devices[0];
        _microphoneClip = Microphone.Start(_microphoneName, true, 1, 44100);
        _microphoneInitialized = true;
    }

    private void Update()
    {
        if (!_microphoneInitialized) return;
        // Только локальный игрок проверяет микрофон
        if (NetworkClient.active && !isLocalPlayer) return;

        float volume = GetMicrophoneVolume();
        float volumeChange = Mathf.Abs(volume - _lastVolume);

        // Срабатывает при изменении громкости больше минимального шага и выше порога
        if (volumeChange >= _minVolumeChangeStep && volume > _volumeThreshold)
        {
            TriggerActiveEchoFromMicrophone();
            _lastVolume = volume;
            _lastEchoTime = Time.time;
        }
        // Если громкость выше порога, но шаг не пройден - срабатываем раз в N секунд
        else if (volume > _volumeThreshold && Time.time - _lastEchoTime >= _echoIntervalWhenLoud)
        {
            TriggerActiveEchoFromMicrophone();
            _lastEchoTime = Time.time;
        }
        // Обновляем последнюю громкость если она упала ниже порога
        else if (volume <= _volumeThreshold)
        {
            _lastVolume = volume;
        }
    }

    private float GetMicrophoneVolume()
    {
        if (_microphoneClip == null) return 0f;

        int micPosition = Microphone.GetPosition(_microphoneName) - _sampleWindow;
        if (micPosition < 0) return 0f;

        float[] samples = new float[_sampleWindow];
        _microphoneClip.GetData(samples, micPosition);

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

        if (_microphoneInitialized && !string.IsNullOrEmpty(_microphoneName))
        {
            Microphone.End(_microphoneName);
            _microphoneInitialized = false;
        }

        if (_playerController != null)
            _playerController.OnFootstep -= OnFootstep;

        if (_fpsController != null)
            _fpsController.OnFootstep -= OnFootstep;
    }

    private void OnEchoPerformed(InputAction.CallbackContext ctx)
    {
        TriggerActiveEcho();
    }

    private void TriggerActiveEcho()
    {
        // Только локальный игрок может активировать эхо
        if (NetworkClient.active && !isLocalPlayer) return;
        if (_activePingPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(transform.position, _activePingPreset);
    }

    private void TriggerActiveEchoFromMicrophone()
    {
        // Только локальный игрок может активировать эхо
        if (NetworkClient.active && !isLocalPlayer) return;

        if (_activePingPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(transform.position, _activePingPreset);
    }

    private void OnFootstep(Vector3 position, bool isSprinting)
    {
        // Только локальный игрок генерирует эхо от шагов
        if (NetworkClient.active && !isLocalPlayer) return;

        if (EchoManager.Instance == null) return;

        // Crouching = silent, no passive echo
        bool isCrouching = (_playerController != null && _playerController.IsCrouching) ||
                          (_fpsController != null && _fpsController.IsCrouching);
        if (isCrouching) return;

        var preset = isSprinting && _sprintFootstepPreset != null ? _sprintFootstepPreset : _footstepPreset;
        if (preset == null) return;

        EchoManager.Instance.SpawnEcho(position, preset);
    }
}

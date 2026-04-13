using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// Player echo locator for multiplayer with Mirror.
/// Active echo: LMB (Attack action) with cooldown or loud sound into microphone.
/// Использует SharedMicrophone для доступа к микрофону (на том же игроке или LocalInstance).
/// </summary>
public class PlayerEchoLocator : NetworkBehaviour
{
    [Header("Config")]
    static private EchoLocatorConfig _config;

    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Microphone Detection")]
    [SerializeField] private float _volumeThreshold = 0.01f;
    [SerializeField] private int _sampleWindow = 128;
    [SerializeField] private float _minVolumeChangeStep = 0.005f;
    [SerializeField] private float _echoIntervalWhenLoud = 0.5f;

    [Header("Active Echo Cooldown")]
    [SerializeField] private float _activeEchoCooldown = 0.5f;

    private InputAction _echoAction;
    private SharedMicrophone _microphone;

    private float _lastVolume;
    private float _lastEchoTime;
    private float _lastActiveEchoTime;

    private void Awake()
    {
        _microphone = GetComponent<SharedMicrophone>();
        if(_config == null) 
            _config = Resources.Load<EchoLocatorConfig>("EchoLocatorConfig");
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetupInput();
    }

    private void OnEnable()
    {
        // Для синглплеера
        if (!NetworkClient.active)
        {
            SetupInput();
        }
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

    /// <summary>
    /// Получает активный микрофон (локальный компонент или LocalInstance).
    /// </summary>
    private SharedMicrophone GetMicrophone()
    {
        // Сначала проверяем локальный компонент на этом игроке
        if (_microphone != null && _microphone.IsRecording)
            return _microphone;

        // Иначе используем глобальный LocalInstance
        return SharedMicrophone.LocalInstance;
    }

    private void Update()
    {
        // Только локальный игрок может использовать ввод
        if (NetworkClient.active && !isLocalPlayer) return;

        // Проверка нажатия левой кнопки мыши напрямую
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TriggerActiveEchoWithCooldown();
        }

        var mic = GetMicrophone();
        if (mic == null || !mic.IsRecording || mic.Clip == null) return;

        float volume = GetMicrophoneVolume(mic);
        float volumeChange = Mathf.Abs(volume - _lastVolume);

        // Срабатывает при изменении громкости больше минимального шага и выше порога
        if (volumeChange >= _minVolumeChangeStep && volume > _volumeThreshold)
        {
            TriggerActiveEchoFromMicrophone();
            _lastVolume = volume;
            _lastEchoTime = Time.time;
        }
        // Если громкость выше порога, но шаг не пройден - срабатыем раз в N секунд
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
    }

    private void OnEchoPerformed(InputAction.CallbackContext ctx)
    {
        TriggerActiveEchoWithCooldown();
    }

    private void TriggerActiveEchoWithCooldown()
    {
        // Проверка кулдауна
        if (Time.time - _lastActiveEchoTime < _activeEchoCooldown) return;

        TriggerActiveEcho();
        _lastActiveEchoTime = Time.time;
    }

    private void TriggerActiveEcho()
    {
        // Только локальный игрок может активировать эхо
        if (NetworkClient.active && !isLocalPlayer) return;
        if (_config == null || _config.ActivePingPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(transform.position, _config.ActivePingPreset);
    }

    private void TriggerActiveEchoFromMicrophone()
    {
        // Только локальный игрок может активировать эхо
        if (NetworkClient.active && !isLocalPlayer) return;

        if (_config == null || _config.ActivePingPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(transform.position, _config.ActivePingPreset);
    }
}

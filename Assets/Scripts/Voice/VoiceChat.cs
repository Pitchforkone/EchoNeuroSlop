using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Vivox;

/// <summary>
/// Компонент голосового чата на базе Vivox.
/// Автоматически подключается к голосовому каналу при спавне игрока.
///
/// Поддерживает Push-to-Talk (по умолчанию) и Voice Activity Detection (VAD).
/// Сохраняет публичный API для совместимости с VoiceChatUI.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class VoiceChat : NetworkBehaviour
{
    // ========================= Настройки =========================

    [Header("Режим передачи")]
    [Tooltip("Если true — голос передаётся только при зажатии кнопки (Push-to-Talk).\n" +
             "Если false — голосовая активация (VAD).")]
    [SerializeField] private bool _pushToTalk = true;

    [Tooltip("Кнопка Push-to-Talk")]
    [SerializeField] private Key _pttKey = Key.T;

    [Header("Звук")]
    [Tooltip("Громкость воспроизведения входящего голоса (0-100)")]
    [SerializeField, Range(0, 100)] private int _playbackVolume = 50;

    [Header("Vivox канал")]
    [Tooltip("Имя голосового канала Vivox. Все игроки с одинаковым именем слышат друг друга.")]
    [SerializeField] private string _channelName = "GameVoice";

    [Tooltip("Тип канала: Positional (3D) или NonPositional (обычный)")]
    [SerializeField] private bool _use3DPositional = true;

    [Header("3D-звук (Positional)")]
    [Tooltip("Максимальная дистанция слышимости голоса")]
    [SerializeField] private int _audibleDistance = 30;

    [Tooltip("Дистанция полной громкости (без затухания)")]
    [SerializeField] private int _conversationalDistance = 5;

    [Tooltip("Fade intensity (1.0 = линейное затухание)")]
    [SerializeField] private float _audioFadeIntensity = 1.0f;

    // ========================= Публичные свойства =========================

    /// <summary>Говорит ли локальный игрок прямо сейчас.</summary>
    public bool IsTransmitting { get; private set; }

    /// <summary>Воспроизводится ли входящий голос другого игрока.</summary>
    public bool IsPlayingVoice { get; private set; }

    /// <summary>Событие: изменилось состояние передачи (true = голос передаётся).</summary>
    public event Action<bool> OnTransmitStateChanged;

    /// <summary>Событие: изменилось состояние воспроизведения входящего голоса.</summary>
    public event Action<bool> OnPlaybackStateChanged;

    // ========================= Приватные поля =========================

    private Keyboard _keyboard;
    private bool _wasTransmitting;
    private bool _wasPlaying;
    private bool _isLoggedIn;
    private bool _isInChannel;
    private bool _isMuted;
    private bool _localPlayerInitialized;

    private static bool _servicesInitialized;
    private static bool _vivoxInitialized;
    private static bool _authenticated;

    // ========================= Жизненный цикл =========================

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        StartCoroutine(InitializeAndJoin());
    }

    private void Start()
    {
        // Для оффлайн-режима
        if (!NetworkClient.active)
        {
            StartCoroutine(InitializeAndJoin());
        }
    }

    private IEnumerator InitializeAndJoin()
    {
        if (_localPlayerInitialized) yield break;
        _localPlayerInitialized = true;

        _keyboard = Keyboard.current;

        // 1. Инициализация Unity Services (один раз)
        if (!_servicesInitialized)
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var initTask = UnityServices.InitializeAsync();
                while (!initTask.IsCompleted) yield return null;

                if (initTask.IsFaulted)
                {
                    Debug.LogError($"[VoiceChat] Unity Services initialization failed: {initTask.Exception}");
                    yield break;
                }
            }
            _servicesInitialized = true;
 
        }

        // 2. Аутентификация через Unity Authentication (требуется для Vivox)
        if (!_authenticated)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                var authTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
                while (!authTask.IsCompleted) yield return null;

                if (authTask.IsFaulted)
                {
                    Debug.LogError($"[VoiceChat] Authentication failed: {authTask.Exception}");
                    yield break;
                }
            }
            _authenticated = true;
        }

        // 3. Инициализация Vivox (один раз)
        if (!_vivoxInitialized)
        {
            var vivoxInitTask = VivoxService.Instance.InitializeAsync();
            while (!vivoxInitTask.IsCompleted) yield return null;

            if (vivoxInitTask.IsFaulted)
            {
                Debug.LogError($"[VoiceChat] Vivox initialization failed: {vivoxInitTask.Exception}");
                yield break;
            }

            _vivoxInitialized = true;
        }

        // 4. Login
        if (!_isLoggedIn)
        {
            yield return LoginToVivox();
        }

        // 5. Join channel
        if (_isLoggedIn && !_isInChannel)
        {
            yield return JoinChannel();
        }

        // 6. Настройка режима передачи
        ApplyTransmissionMode();
        ApplyVolume();
    }

    private IEnumerator LoginToVivox()
    {
        string playerId;

        if (NetworkClient.active)
        {
            playerId = $"Player_{netId}";
        }
        else
        {
            playerId = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
        }

        var loginOptions = new LoginOptions()
        {
            DisplayName = playerId
        };

        var loginTask = VivoxService.Instance.LoginAsync(loginOptions);
        while (!loginTask.IsCompleted) yield return null;

        if (loginTask.IsFaulted)
        {
            Debug.LogError($"[VoiceChat] Vivox login failed: {loginTask.Exception}");
            yield break;
        }

        _isLoggedIn = true;
    }

    private IEnumerator JoinChannel()
    {
        ChatCapability capability = ChatCapability.AudioOnly;

        if (_use3DPositional)
        {
            Channel3DProperties spatialProps = new Channel3DProperties(
                _audibleDistance,
                _conversationalDistance,
                _audioFadeIntensity,
                AudioFadeModel.InverseByDistance
            );

            var joinTask = VivoxService.Instance.JoinPositionalChannelAsync(
                _channelName, capability, spatialProps);
            while (!joinTask.IsCompleted) yield return null;

            if (joinTask.IsFaulted)
            {
                Debug.LogError($"[VoiceChat] Failed to join positional channel: {joinTask.Exception}");
                yield break;
            }
        }
        else
        {
            var joinTask = VivoxService.Instance.JoinGroupChannelAsync(
                _channelName, capability);
            while (!joinTask.IsCompleted) yield return null;

            if (joinTask.IsFaulted)
            {
                Debug.LogError($"[VoiceChat] Failed to join group channel: {joinTask.Exception}");
                yield break;
            }
        }

        _isInChannel = true;

        // Подписываемся на события участников для отслеживания говорящих
        VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
        VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
    }

    private void OnParticipantAdded(VivoxParticipant participant)
    {
        if (!participant.IsSelf)
        {
            participant.ParticipantSpeechDetected += () => UpdatePlaybackState();
        }
    }

    private void OnParticipantRemoved(VivoxParticipant participant)
    {
        // VivoxParticipant events are auto-cleaned by Vivox SDK on removal
        UpdatePlaybackState();
    }

    private void UpdatePlaybackState()
    {
        if (!_isInChannel || VivoxService.Instance == null) return;

        bool anyoneSpeaking = false;

        try
        {
            if (VivoxService.Instance.ActiveChannels.ContainsKey(_channelName))
            {
                foreach (var participant in VivoxService.Instance.ActiveChannels[_channelName])
                {
                    if (!participant.IsSelf && participant.SpeechDetected)
                    {
                        anyoneSpeaking = true;
                        break;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Channel may not be ready
        }

        if (anyoneSpeaking != _wasPlaying)
        {
            _wasPlaying = anyoneSpeaking;
            IsPlayingVoice = anyoneSpeaking;
            OnPlaybackStateChanged?.Invoke(anyoneSpeaking);
        }
    }

    // ========================= Update =========================

    private void Update()
    {
        // Только для локального игрока
        if (NetworkClient.active && !isLocalPlayer) return;

        if (!_isInChannel) return;

        if (_keyboard == null)
            _keyboard = Keyboard.current;

        // Push-to-Talk
        if (_pushToTalk)
        {
            bool isPressed = _keyboard != null && _keyboard[_pttKey].isPressed;

            if (isPressed && !_wasTransmitting)
            {
                SetTransmitting(true);
            }
            else if (!isPressed && _wasTransmitting)
            {
                SetTransmitting(false);
            }
        }

        // Обновляем 3D-позицию
        if (_use3DPositional && _isInChannel)
        {
            try
            {
                VivoxService.Instance.Set3DPosition(gameObject, _channelName);
            }
            catch (Exception)
            {
                // Игнорируем ошибки если канал ещё не готов
            }
        }

        // В режиме VAD проверяем состояние через Vivox
        if (!_pushToTalk)
        {
            bool speaking = false;
            try
            {
                if (VivoxService.Instance.ActiveChannels.ContainsKey(_channelName))
                {
                    foreach (var p in VivoxService.Instance.ActiveChannels[_channelName])
                    {
                        if (p.IsSelf && p.SpeechDetected)
                        {
                            speaking = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore
            }

            if (speaking != _wasTransmitting)
            {
                _wasTransmitting = speaking;
                IsTransmitting = speaking;
                OnTransmitStateChanged?.Invoke(speaking);
            }
        }
    }

    private void SetTransmitting(bool transmitting)
    {
        _wasTransmitting = transmitting;
        IsTransmitting = transmitting;

        try
        {
            if (transmitting)
            {
                VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.All);
                VivoxService.Instance.UnmuteInputDevice();
            }
            else
            {
                VivoxService.Instance.MuteInputDevice();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VoiceChat] SetTransmitting error: {e.Message}");
        }

        OnTransmitStateChanged?.Invoke(transmitting);
    }

    // ========================= Настройки режимов =========================

    private void ApplyTransmissionMode()
    {
        if (!_isInChannel) return;

        try
        {
            if (_pushToTalk)
            {
                // В PTT режиме начинаем с выключенным микрофоном
                VivoxService.Instance.MuteInputDevice();
            }
            else
            {
                // В VAD режиме микрофон всегда включен, Vivox сам детектит голос
                VivoxService.Instance.UnmuteInputDevice();
                VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.All);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VoiceChat] ApplyTransmissionMode error: {e.Message}");
        }
    }

    private void ApplyVolume()
    {
        if (!_isInChannel) return;

        try
        {
            VivoxService.Instance.SetOutputDeviceVolume(_playbackVolume);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VoiceChat] ApplyVolume error: {e.Message}");
        }
    }

    // ========================= Публичные методы =========================

    /// <summary>
    /// Включить/выключить Push-to-Talk режим.
    /// </summary>
    public void SetPushToTalk(bool enabled)
    {
        _pushToTalk = enabled;
        ApplyTransmissionMode();
    }

    /// <summary>
    /// Установить кнопку Push-to-Talk.
    /// </summary>
    public void SetPTTKey(Key key)
    {
        _pttKey = key;
    }

    /// <summary>
    /// Установить громкость воспроизведения (0-2). Пересчитывается в Vivox (0-100).
    /// </summary>
    public void SetPlaybackVolume(float volume)
    {
        float clamped = Mathf.Clamp(volume, 0f, 2f);
        _playbackVolume = Mathf.RoundToInt((clamped / 2f) * 100f);
        ApplyVolume();
    }

    /// <summary>
    /// Установить порог VAD (0-1). В Vivox VAD работает автоматически.
    /// </summary>
    public void SetVADThreshold(float threshold)
    {
        // Vivox использует внутренний VAD; порог настраивается через VivoxConfigurationOptions
        // при инициализации. Для runtime настройки этот параметр не поддерживается напрямую.
    }

    /// <summary>
    /// Заглушить/разглушить входящий голос (mute).
    /// </summary>
    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        try
        {
            if (_isInChannel)
            {
                if (muted)
                    VivoxService.Instance.MuteOutputDevice();
                else
                    VivoxService.Instance.UnmuteOutputDevice();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VoiceChat] SetMuted error: {e.Message}");
        }
    }

    // ========================= Очистка =========================

    public override void OnStopClient()
    {
        base.OnStopClient();
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        IsTransmitting = false;
        IsPlayingVoice = false;

        if (_isInChannel)
        {
            try
            {
                VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
                VivoxService.Instance.LeaveChannelAsync(_channelName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoiceChat] Cleanup channel error: {e.Message}");
            }
            _isInChannel = false;
        }

        if (_isLoggedIn)
        {
            try
            {
                VivoxService.Instance.LogoutAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoiceChat] Cleanup logout error: {e.Message}");
            }
            _isLoggedIn = false;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// Компонент голосового чата для мультиплеера.
/// Вешается на префаб игрока рядом с SharedMicrophone.
///
/// Локальный игрок: захватывает аудио с микрофона, сжимает в PCM16-байты,
/// отправляет чанками через Mirror Command ? ClientRpc.
///
/// Удалённый игрок: принимает аудио-чанки, помещает в кольцевой буфер
/// и воспроизводит через AudioSource с 3D-позиционированием.
///
/// Поддерживает Push-to-Talk (по кнопке) и Voice Activity Detection (VAD).
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class VoiceChat : NetworkBehaviour
{
    // ??????????????????????? Настройки ???????????????????????

    [Header("Режим активации")]
    [Tooltip("Если true — нужно зажимать кнопку для передачи голоса (Push-to-Talk).\n" +
             "Если false — передача включена всегда когда громкость выше порога (VAD).")]
    [SerializeField] private bool _pushToTalk = true;

    [Tooltip("Клавиша Push-to-Talk")]
    [SerializeField] private Key _pttKey = Key.T;

    [Header("VAD (Voice Activity Detection)")]
    [Tooltip("Минимальный RMS-уровень для включения передачи (0-1)")]
    [SerializeField] private float _vadThreshold = 0.01f;

    [Tooltip("Время удержания передачи после падения громкости ниже порога (сек)")]
    [SerializeField] private float _vadHoldTime = 0.3f;

    [Header("Аудио")]
    [Tooltip("Длина одного чанка в миллисекундах (20-100). Меньше = ниже задержка, больше трафик.")]
    [SerializeField] private int _chunkMs = 20;

    [Tooltip("Громкость воспроизведения входящего голоса (0-2)")]
    [SerializeField] private float _playbackVolume = 1f;

    [Tooltip("Даунсэмплинг: передавать аудио с пониженной частотой для экономии трафика.\n" +
             "8000 — телефонное качество (~50%% трафика). 0 — без даунсэмплинга.")]
    [SerializeField] private int _transmitSampleRate = 8000;

    [Header("Джиттер-буфер")]
    [Tooltip("Количество чанков, которые нужно накопить перед началом воспроизведения.\n" +
             "Больше = стабильнее, но выше задержка. 2-4 рекомендуется.")]
    [SerializeField] private int _jitterBufferSize = 3;

    [Header("3D-звук")]
    [Tooltip("Минимальная дистанция 3D-звука")]
    [SerializeField] private float _minDistance = 1f;

    [Tooltip("Максимальная дистанция 3D-звука. За ней голос не слышен.")]
    [SerializeField] private float _maxDistance = 30f;

    // ??????????????????????? Публичные свойства ???????????????????????

    /// <summary>Передаёт ли локальный игрок голос прямо сейчас.</summary>
    public bool IsTransmitting { get; private set; }

    /// <summary>Воспроизводит ли удалённый игрок голос прямо сейчас.</summary>
    public bool IsPlayingVoice { get; private set; }

    /// <summary>Событие: изменилось состояние передачи (true = начал говорить).</summary>
    public event Action<bool> OnTransmitStateChanged;

    /// <summary>Событие: изменилось состояние воспроизведения удалённого голоса.</summary>
    public event Action<bool> OnPlaybackStateChanged;

    // ??????????????????????? Приватные поля ???????????????????????

    // --- Локальный игрок (отправка) ---
    private SharedMicrophone _microphone;
    private int _lastMicPosition;
    private int _chunkSizeSamples;          // размер чанка в семплах
    private readonly List<float> _sendBuffer = new List<float>();
    private Keyboard _keyboard;
    private float _vadTimer;
    private bool _wasTransmitting;

    // --- Удалённый игрок (приём) ---
    private AudioSource _audioSource;
    private AudioClip _playbackClip;
    private int _playbackWritePos;
    private float _playbackSilenceTimer;
    private bool _wasPlaying;
    private int _playbackSampleRate;
    private bool _playbackStarted;
    private int _jitterChunksReceived;
    private readonly Queue<float[]> _jitterQueue = new Queue<float[]>();

    // Константы
    private const int PlaybackBufferSeconds = 2;   // длина буфера воспроизведения
    private const float PlaybackSilenceTimeout = 0.5f;

    // ??????????????????????? Жизненный цикл ???????????????????????

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetupLocal();
    }

    private void Start()
    {
        // Для одиночной игры (без Mirror)
        if (!NetworkClient.active)
        {
            SetupLocal();
        }

        // Для удалённых игроков — настраиваем воспроизведение
        if (NetworkClient.active && !isLocalPlayer)
        {
            SetupRemotePlayback();
        }
    }

    private void SetupLocal()
    {
        _microphone = GetComponent<SharedMicrophone>();
        if (_microphone == null)
            _microphone = SharedMicrophone.LocalInstance;

        _keyboard = Keyboard.current;

        if (_microphone != null)
        {
            _chunkSizeSamples = (_microphone.SampleRate * _chunkMs) / 1000;
        }
    }

    private void SetupRemotePlayback()
    {
        // Создаём AudioSource для воспроизведения голоса удалённого игрока
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 1f;        // полностью 3D
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = _minDistance;
        _audioSource.maxDistance = _maxDistance;
        _audioSource.volume = _playbackVolume;
        _audioSource.loop = true;
        _audioSource.dopplerLevel = 0f;
        _audioSource.playOnAwake = false;
        _audioSource.priority = 0;             // высший приоритет
    }

    private void Update()
    {
        if (NetworkClient.active && !isLocalPlayer)
        {
            UpdateRemotePlayback();
            return;
        }

        if (!NetworkClient.active || isLocalPlayer)
        {
            UpdateLocalTransmit();
        }
    }

    // ??????????????????????? Локальный игрок — передача ???????????????????????

    private void UpdateLocalTransmit()
    {
        if (_microphone == null || !_microphone.IsRecording || _microphone.Clip == null)
            return;

        if (_keyboard == null)
            _keyboard = Keyboard.current;

        // Определяем, нужно ли передавать
        bool shouldTransmit = ShouldTransmit();

        // Обновляем состояние
        if (shouldTransmit != _wasTransmitting)
        {
            _wasTransmitting = shouldTransmit;
            IsTransmitting = shouldTransmit;
            OnTransmitStateChanged?.Invoke(shouldTransmit);
        }

        if (!shouldTransmit)
        {
            // Сбрасываем позицию чтобы не накапливать буфер
            _lastMicPosition = _microphone.GetPosition();
            _sendBuffer.Clear();
            return;
        }

        // Читаем новые семплы из микрофона
        int currentPos = _microphone.GetPosition();
        if (currentPos == _lastMicPosition) return;

        int totalSamples = _microphone.Clip.samples;
        int samplesToRead;

        if (currentPos > _lastMicPosition)
        {
            samplesToRead = currentPos - _lastMicPosition;
        }
        else
        {
            samplesToRead = (totalSamples - _lastMicPosition) + currentPos;
        }

        float[] samples = new float[samplesToRead];
        _microphone.Clip.GetData(samples, _lastMicPosition);
        _lastMicPosition = currentPos;

        // Добавляем в буфер отправки
        _sendBuffer.AddRange(samples);

        // Отправляем чанками
        while (_sendBuffer.Count >= _chunkSizeSamples)
        {
            float[] chunk = new float[_chunkSizeSamples];
            _sendBuffer.CopyTo(0, chunk, 0, _chunkSizeSamples);
            _sendBuffer.RemoveRange(0, _chunkSizeSamples);

            // Даунсэмплинг для уменьшения трафика
            float[] toSend = Downsample(chunk, _microphone.SampleRate, _transmitSampleRate);

            byte[] pcmBytes = FloatToPCM16(toSend);
            CmdSendVoiceData(pcmBytes, _transmitSampleRate > 0 ? _transmitSampleRate : _microphone.SampleRate);
        }
    }

    private bool ShouldTransmit()
    {
        if (_pushToTalk)
        {
            // Push-to-Talk: передаём только пока зажата кнопка
            return _keyboard != null && _keyboard[_pttKey].isPressed;
        }
        else
        {
            // VAD: передаём если громкость выше порога
            float rms = GetCurrentRMS();
            if (rms >= _vadThreshold)
            {
                _vadTimer = _vadHoldTime;
                return true;
            }
            else
            {
                _vadTimer -= Time.deltaTime;
                return _vadTimer > 0f;
            }
        }
    }

    private float GetCurrentRMS()
    {
        if (_microphone == null || !_microphone.IsRecording || _microphone.Clip == null)
            return 0f;

        int pos = _microphone.GetPosition();
        int samplesToCheck = Mathf.Min(256, _microphone.Clip.samples);
        int readPos = pos - samplesToCheck;
        if (readPos < 0) readPos += _microphone.Clip.samples;
        readPos = Mathf.Clamp(readPos, 0, _microphone.Clip.samples - samplesToCheck);

        float[] samples = new float[samplesToCheck];
        _microphone.Clip.GetData(samples, readPos);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / samples.Length);
    }

    // ??????????????????????? Сеть ???????????????????????

    /// <summary>
    /// Клиент отправляет чанк аудио на сервер.
    /// </summary>
    [Command(channel = Channels.Unreliable)]
    private void CmdSendVoiceData(byte[] pcmData, int sampleRate)
    {
        // Сервер пересылает всем клиентам кроме отправителя
        RpcReceiveVoiceData(pcmData, sampleRate);
    }

    /// <summary>
    /// Сервер рассылает аудио всем клиентам.
    /// </summary>
    [ClientRpc(includeOwner = false)]
    private void RpcReceiveVoiceData(byte[] pcmData, int sampleRate)
    {
        ReceiveVoiceChunk(pcmData, sampleRate);
    }

    // ??????????????????????? Удалённый игрок — воспроизведение ???????????????????????

    private void ReceiveVoiceChunk(byte[] pcmData, int sampleRate)
    {
        if (_audioSource == null) return;

        float[] samples = PCM16ToFloat(pcmData);
        if (samples.Length == 0) return;

        // Создаём клип-буфер при первом получении или если sampleRate изменился
        if (_playbackClip == null || _playbackSampleRate != sampleRate)
        {
            if (_playbackClip != null)
            {
                _audioSource.Stop();
                Destroy(_playbackClip);
            }

            _playbackSampleRate = sampleRate;
            _playbackClip = AudioClip.Create(
                "VoiceChatPlayback",
                sampleRate * PlaybackBufferSeconds,
                1,
                sampleRate,
                false
            );
            _playbackWritePos = 0;
            _playbackStarted = false;
            _jitterChunksReceived = 0;
            _jitterQueue.Clear();
            _audioSource.clip = _playbackClip;
        }

        // Джиттер-буфер: накапливаем несколько чанков перед стартом воспроизведения
        if (!_playbackStarted)
        {
            _jitterQueue.Enqueue(samples);
            _jitterChunksReceived++;

            if (_jitterChunksReceived >= _jitterBufferSize)
            {
                // Записываем все накопленные чанки и стартуем
                while (_jitterQueue.Count > 0)
                {
                    WriteToPlaybackBuffer(_jitterQueue.Dequeue());
                }
                _audioSource.Play();
                _playbackStarted = true;
            }
        }
        else
        {
            WriteToPlaybackBuffer(samples);
        }

        _playbackSilenceTimer = PlaybackSilenceTimeout;
    }

    private void WriteToPlaybackBuffer(float[] samples)
    {
        int clipSamples = _playbackClip.samples;

        if (_playbackWritePos + samples.Length <= clipSamples)
        {
            _playbackClip.SetData(samples, _playbackWritePos);
        }
        else
        {
            // Оборачиваем через конец буфера
            int firstPart = clipSamples - _playbackWritePos;
            float[] first = new float[firstPart];
            Array.Copy(samples, 0, first, 0, firstPart);
            _playbackClip.SetData(first, _playbackWritePos);

            int secondPart = samples.Length - firstPart;
            float[] second = new float[secondPart];
            Array.Copy(samples, firstPart, second, 0, secondPart);
            _playbackClip.SetData(second, 0);
        }

        _playbackWritePos = (_playbackWritePos + samples.Length) % clipSamples;
    }

    private void UpdateRemotePlayback()
    {
        if (_audioSource == null) return;

        bool isPlaying = _playbackSilenceTimer > 0f;

        if (_playbackSilenceTimer > 0f)
        {
            _playbackSilenceTimer -= Time.deltaTime;

            // Если тишина закончилась — останавливаем и сбрасываем джиттер-буфер
            if (_playbackSilenceTimer <= 0f)
            {
                _audioSource.Stop();
                _playbackStarted = false;
                _jitterChunksReceived = 0;
                _jitterQueue.Clear();
                _playbackWritePos = 0;
            }
        }

        // Обновляем громкость на лету
        _audioSource.volume = _playbackVolume;

        // Обновляем состояние
        if (isPlaying != _wasPlaying)
        {
            _wasPlaying = isPlaying;
            IsPlayingVoice = isPlaying;
            OnPlaybackStateChanged?.Invoke(isPlaying);
        }
    }

    // ??????????????????????? Утилиты конвертации ???????????????????????

    private static byte[] FloatToPCM16(float[] floatSamples)
    {
        byte[] bytes = new byte[floatSamples.Length * 2];
        for (int i = 0; i < floatSamples.Length; i++)
        {
            float clamped = Mathf.Clamp(floatSamples[i], -1f, 1f);
            short val = (short)(clamped * 32767f);
            bytes[i * 2] = (byte)(val & 0xFF);
            bytes[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        return bytes;
    }

    private static float[] PCM16ToFloat(byte[] pcmBytes)
    {
        if (pcmBytes == null || pcmBytes.Length < 2) return Array.Empty<float>();

        int sampleCount = pcmBytes.Length / 2;
        float[] floats = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short val = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
            floats[i] = val / 32767f;
        }
        return floats;
    }

    /// <summary>
    /// Простой даунсэмплинг: берём каждый N-й семпл.
    /// Если targetRate <= 0 или >= sourceRate — возвращает исходный массив.
    /// </summary>
    private static float[] Downsample(float[] samples, int sourceRate, int targetRate)
    {
        if (targetRate <= 0 || targetRate >= sourceRate)
            return samples;

        int ratio = sourceRate / targetRate;
        int newLength = samples.Length / ratio;
        float[] result = new float[newLength];
        for (int i = 0; i < newLength; i++)
        {
            result[i] = samples[i * ratio];
        }
        return result;
    }

    // ??????????????????????? Публичные методы ???????????????????????

    /// <summary>
    /// Включить/выключить Push-to-Talk режим.
    /// </summary>
    public void SetPushToTalk(bool enabled)
    {
        _pushToTalk = enabled;
    }

    /// <summary>
    /// Установить клавишу Push-to-Talk.
    /// </summary>
    public void SetPTTKey(Key key)
    {
        _pttKey = key;
    }

    /// <summary>
    /// Установить громкость воспроизведения (0-2).
    /// </summary>
    public void SetPlaybackVolume(float volume)
    {
        _playbackVolume = Mathf.Clamp(volume, 0f, 2f);
    }

    /// <summary>
    /// Установить порог VAD (0-1).
    /// </summary>
    public void SetVADThreshold(float threshold)
    {
        _vadThreshold = Mathf.Clamp01(threshold);
    }

    /// <summary>
    /// Отключить/включить голосовой чат целиком (mute).
    /// </summary>
    public void SetMuted(bool muted)
    {
        if (_audioSource != null)
            _audioSource.mute = muted;
    }

    // ??????????????????????? Очистка ???????????????????????

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

        if (_playbackClip != null)
        {
            if (_audioSource != null)
                _audioSource.Stop();
            Destroy(_playbackClip);
            _playbackClip = null;
        }
    }
}

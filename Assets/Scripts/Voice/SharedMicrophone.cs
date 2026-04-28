using UnityEngine;
using Mirror;

/// <summary>
/// Общий компонент для работы с микрофоном.
/// Запускает запись один раз для локального игрока.
/// Другие скрипты получают доступ через статический LocalInstance.
/// Вешается на объект игрока.
/// </summary>
public class SharedMicrophone : NetworkBehaviour
{
    /// <summary>
    /// Ссылка на экземпляр локального игрока.
    /// Доступен только на клиенте для своего объекта.
    /// </summary>
    public static SharedMicrophone LocalInstance { get; private set; }

    /// <summary>
    /// Синоним LocalInstance для обратной совместимости.
    /// </summary>
    public static SharedMicrophone Instance => LocalInstance;

    [Tooltip("Частота дискретизации микрофона")]
    [SerializeField] private int _sampleRate = 16000;

    [Tooltip("Длина кольцевого буфера записи (секунды)")]
    [SerializeField] private int _bufferLengthSec = 1;

    public AudioClip Clip { get; private set; }
    public string DeviceName { get; private set; }
    public int SampleRate => _sampleRate;
    public bool IsRecording { get; private set; }

    private void Awake()
    {
        // В оффлайн-режиме инициализируем сразу
        if (!NetworkClient.active)
        {
            InitializeAsLocal();
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        InitializeAsLocal();
    }

    private void InitializeAsLocal()
    {
        // Если уже есть активный экземпляр, останавливаем старый
        if (LocalInstance != null && LocalInstance != this)
        {
            LocalInstance.StopRecording();
        }

        LocalInstance = this;
        StartRecording();
    }

    private void Start()
    {
        // Для оффлайн-режима: если запись ещё не началась
        if (!NetworkClient.active && !IsRecording)
        {
            StartRecording();
        }
    }

    public void StartRecording()
    {
        // Запись только для локального игрока
        if (NetworkClient.active && !isLocalPlayer) return;

        if (IsRecording) return;

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[SharedMicrophone] Микрофон не найден!");
            return;
        }

        DeviceName = Microphone.devices[0];
        Clip = Microphone.Start(DeviceName, true, _bufferLengthSec, _sampleRate);
        IsRecording = true;
    }

    public void StopRecording()
    {
        if (!IsRecording) return;

        if (!string.IsNullOrEmpty(DeviceName))
            Microphone.End(DeviceName);

        Clip = null;
        IsRecording = false;
    }

    /// <summary>
    /// Текущая позиция записи в буфере.
    /// </summary>
    public int GetPosition()
    {
        if (!IsRecording || string.IsNullOrEmpty(DeviceName)) return 0;
        return Microphone.GetPosition(DeviceName);
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        CleanupLocal();
    }

    private void OnDisable()
    {
        // Для оффлайн-режима
        if (!NetworkClient.active)
        {
            CleanupLocal();
        }
    }

    private void OnDestroy()
    {
        CleanupLocal();
    }

    private void CleanupLocal()
    {
        StopRecording();
        if (LocalInstance == this)
        {
            LocalInstance = null;
        }
    }
}

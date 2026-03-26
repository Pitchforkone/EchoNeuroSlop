using UnityEngine;
using Mirror;

/// <summary>
/// Компонент микрофона для игрока в мультиплеере.
/// Запускает запись только для локального игрока.
/// Другие скрипты могут получить доступ через статическое свойство LocalInstance.
/// Повесить на префаб игрока.
/// </summary>
public class SharedMicrophone : NetworkBehaviour
{
    /// <summary>
    /// Ссылка на микрофон локального игрока.
    /// Доступна только на клиенте для локального игрока.
    /// </summary>
    public static SharedMicrophone LocalInstance { get; private set; }

    /// <summary>
    /// Устаревшее свойство для обратной совместимости.
    /// Используйте LocalInstance.
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
        // В мультиплеере инициализация происходит в OnStartLocalPlayer
        // В синглплеере - здесь
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
        // Если уже есть локальный микрофон, уничтожаем старый
        if (LocalInstance != null && LocalInstance != this)
        {
            LocalInstance.StopRecording();
        }

        LocalInstance = this;
        StartRecording();
    }

    private void Start()
    {
        // Для синглплеера запускаем запись в Start если ещё не запущена
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
        Debug.Log($"[SharedMicrophone] Запись запущена: {DeviceName}, {_sampleRate} Hz");
    }

    public void StopRecording()
    {
        if (!IsRecording) return;

        if (!string.IsNullOrEmpty(DeviceName))
            Microphone.End(DeviceName);

        Clip = null;
        IsRecording = false;
        Debug.Log("[SharedMicrophone] Запись остановлена");
    }

    /// <summary>
    /// Текущая позиция записи в сэмплах.
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
        // Для синглплеера
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

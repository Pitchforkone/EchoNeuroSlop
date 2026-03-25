using UnityEngine;

/// <summary>
/// Единый менеджер микрофона. Запускает запись один раз,
/// остальные скрипты читают данные через этот синглтон.
/// Повесить на GameObject на сцене (или будет создан автоматически).
/// </summary>
public class SharedMicrophone : MonoBehaviour
{
    public static SharedMicrophone Instance { get; private set; }

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartRecording();
    }

    public void StartRecording()
    {
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
    }

    /// <summary>
    /// Текущая позиция записи в сэмплах.
    /// </summary>
    public int GetPosition()
    {
        if (!IsRecording || string.IsNullOrEmpty(DeviceName)) return 0;
        return Microphone.GetPosition(DeviceName);
    }

    private void OnDestroy()
    {
        StopRecording();
        if (Instance == this)
            Instance = null;
    }
}

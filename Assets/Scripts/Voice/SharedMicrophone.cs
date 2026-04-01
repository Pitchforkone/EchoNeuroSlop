using UnityEngine;
using Photon.Pun;

/// <summary>
/// Общий микрофон для записи и распознавания.
/// Работает только для локального игрока.
/// </summary>
public class SharedMicrophone : MonoBehaviourPun
{
    public static SharedMicrophone LocalInstance { get; private set; }
    public static SharedMicrophone Instance => LocalInstance;

    [SerializeField] private int _sampleRate = 16000;
    [SerializeField] private int _bufferLengthSec = 1;

    public AudioClip Clip { get; private set; }
    public string DeviceName { get; private set; }
    public int SampleRate => _sampleRate;
    public bool IsRecording { get; private set; }

    private void Awake()
    {
        if (!PhotonNetwork.IsConnected)
        {
            InitializeAsLocal();
        }
    }

    private void Start()
    {
        if (PhotonNetwork.IsConnected && photonView.IsMine)
        {
            InitializeAsLocal();
        }
        
        if (!PhotonNetwork.IsConnected && !IsRecording)
        {
            StartRecording();
        }
    }

    private void InitializeAsLocal()
    {
        if (LocalInstance != null && LocalInstance != this)
        {
            LocalInstance.StopRecording();
        }

        LocalInstance = this;
        StartRecording();
    }

    public void StartRecording()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;
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
        Debug.Log("[SharedMicrophone] Запись остановлена");
    }

    public int GetPosition()
    {
        if (!IsRecording || string.IsNullOrEmpty(DeviceName)) return 0;
        return Microphone.GetPosition(DeviceName);
    }

    private void OnDisable()
    {
        if (!PhotonNetwork.IsConnected)
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

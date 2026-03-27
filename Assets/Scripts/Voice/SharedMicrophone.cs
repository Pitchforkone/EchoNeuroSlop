using UnityEngine;
using Mirror;

/// <summary>
/// ��������� ��������� ��� ������ � ������������.
/// ��������� ������ ������ ��� ���������� ������.
/// ������ ������� ����� �������� ������ ����� ����������� �������� LocalInstance.
/// �������� �� ������ ������.
/// </summary>
public class SharedMicrophone : NetworkBehaviour
{
    /// <summary>
    /// ������ �� �������� ���������� ������.
    /// �������� ������ �� ������� ��� ���������� ������.
    /// </summary>
    public static SharedMicrophone LocalInstance { get; private set; }

    /// <summary>
    /// ���������� �������� ��� �������� �������������.
    /// ����������� LocalInstance.
    /// </summary>
    public static SharedMicrophone Instance => LocalInstance;

    [Tooltip("������� ������������� ���������")]
    [SerializeField] private int _sampleRate = 16000;

    [Tooltip("����� ���������� ������ ������ (�������)")]
    [SerializeField] private int _bufferLengthSec = 1;

    public AudioClip Clip { get; private set; }
    public string DeviceName { get; private set; }
    public int SampleRate => _sampleRate;
    public bool IsRecording { get; private set; }

    private void Awake()
    {
        // � ������������ ������������� ���������� � OnStartLocalPlayer
        // � ����������� - �����
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
        // ���� ��� ���� ��������� ��������, ���������� ������
        if (LocalInstance != null && LocalInstance != this)
        {
            LocalInstance.StopRecording();
        }

        LocalInstance = this;
        StartRecording();
    }

    private void Start()
    {
        // ��� ����������� ��������� ������ � Start ���� ��� �� ��������
        if (!NetworkClient.active && !IsRecording)
        {
            StartRecording();
        }
    }

    public void StartRecording()
    {
        // ������ ������ ��� ���������� ������
        if (NetworkClient.active && !isLocalPlayer) return;

        if (IsRecording) return;

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[SharedMicrophone] �������� �� ������!");
            return;
        }

        DeviceName = Microphone.devices[0];
        Clip = Microphone.Start(DeviceName, true, _bufferLengthSec, _sampleRate);
        IsRecording = true;
        Debug.Log($"[SharedMicrophone] ������ ��������: {DeviceName}, {_sampleRate} Hz");
    }

    public void StopRecording()
    {
        if (!IsRecording) return;

        if (!string.IsNullOrEmpty(DeviceName))
            Microphone.End(DeviceName);

        Clip = null;
        IsRecording = false;
        Debug.Log("[SharedMicrophone] ������ �����������");
    }

    /// <summary>
    /// ������� ������� ������ � �������.
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
        // ��� �����������
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

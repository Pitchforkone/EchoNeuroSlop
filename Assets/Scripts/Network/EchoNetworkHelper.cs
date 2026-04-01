using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// Network helper for synchronizing echo spawns across all clients.
/// Uses PhotonNetwork.RaiseEvent instead of RPC — no PhotonView required.
/// Can live on any scene GameObject (e.g. the same object as EchoManager).
/// </summary>
public class EchoNetworkHelper : MonoBehaviour, IOnEventCallback
{
    public static EchoNetworkHelper Instance { get; private set; }

    /// <summary>
    /// Event fired when echo should be spawned locally.
    /// EchoManager subscribes to this.
    /// </summary>
    public static event System.Action<Vector3, float, float, float, Color, float, EchoType> OnNetworkEchoSpawn;

    private const byte EchoSpawnEventCode = 42;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Request to spawn an echo. Will be synchronized to all clients (including sender).
    /// </summary>
    public void RequestSpawnEcho(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime, EchoType echoType = EchoType.Default)
    {
        object[] data = new object[]
        {
            position,
            speed,
            maxRadius,
            intensity,
            color.r,
            color.g,
            color.b,
            color.a,
            lifetime,
            (int)echoType
        };

        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(EchoSpawnEventCode, data, options, SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != EchoSpawnEventCode) return;

        object[] data = (object[])photonEvent.CustomData;

        Vector3 position = (Vector3)data[0];
        float speed = (float)data[1];
        float maxRadius = (float)data[2];
        float intensity = (float)data[3];
        float colorR = (float)data[4];
        float colorG = (float)data[5];
        float colorB = (float)data[6];
        float colorA = (float)data[7];
        float lifetime = (float)data[8];
        int echoTypeInt = (int)data[9];

        Color color = new Color(colorR, colorG, colorB, colorA);
        EchoType echoType = (EchoType)echoTypeInt;

        OnNetworkEchoSpawn?.Invoke(position, speed, maxRadius, intensity, color, lifetime, echoType);
    }
}

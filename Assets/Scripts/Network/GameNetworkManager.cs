using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Handles NGO session lifecycle: hosting, joining by IP, and player spawn points.
/// Place on the same GameObject as NetworkManager and UnityTransport.
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    private const string Tag = "[Net]";

    [Header("Spawn Points")]
    [SerializeField] private Transform[] _spawnPoints;

    private int _nextSpawnIndex;
    private static readonly Dictionary<ulong, (Vector3 pos, Quaternion rot)> _pendingSpawns = new();

    /// <summary>
    /// Called by PlayerController.OnNetworkSpawn on the server to get the assigned spawn point.
    /// </summary>
    public static bool ConsumeSpawnPoint(ulong clientId, out Vector3 position, out Quaternion rotation)
    {
        if (_pendingSpawns.Remove(clientId, out var data))
        {
            position = data.pos;
            rotation = data.rot;
            return true;
        }
        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    private void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError($"{Tag} NetworkManager.Singleton is null! Is NetworkManager in the scene?");
            return;
        }

        // Auto-assign transport if not set in inspector
        if (nm.NetworkConfig.NetworkTransport == null)
        {
            Debug.LogWarning($"{Tag} NetworkTransport not assigned — auto-assigning UnityTransport");
            var transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
                transport = nm.gameObject.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;
        }

        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = ApproveConnection;

        nm.OnClientConnectedCallback += id => Debug.Log($"{Tag} Client connected: {id}");
        nm.OnClientDisconnectCallback += id => Debug.Log($"{Tag} Client disconnected: {id}");
        nm.OnTransportFailure += () => Debug.LogError($"{Tag} Transport failure!");

        Debug.Log($"{Tag} GameNetworkManager initialized. Transport: {nm.NetworkConfig.NetworkTransport.GetType().Name}");
    }

    /// <summary>
    /// Start as host (server + client). ConnectionUI calls this.
    /// </summary>
    public void StartHost()
    {
        var nm = NetworkManager.Singleton;

        // Listen on all network interfaces so LAN clients can connect
        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = "0.0.0.0";
            transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            Debug.Log($"{Tag} Host listen address set to 0.0.0.0:{transport.ConnectionData.Port}");
        }

        Debug.Log($"{Tag} StartHost called. PlayerPrefab: {(nm.NetworkConfig.PlayerPrefab != null ? nm.NetworkConfig.PlayerPrefab.name : "NULL")}");

        _nextSpawnIndex = 0;
        bool result = nm.StartHost();
        Debug.Log($"{Tag} StartHost result: {result}. IsHost={nm.IsHost}, IsListening={nm.IsListening}");
    }

    /// <summary>
    /// Join an existing host by IP address. ConnectionUI calls this.
    /// </summary>
    public void JoinGame(string ipAddress, ushort port = 7777)
    {
        var nm = NetworkManager.Singleton;
        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = ipAddress;
            transport.ConnectionData.Port = port;
            Debug.Log($"{Tag} JoinGame: connecting to {ipAddress}:{port}");
        }
        else
        {
            Debug.LogError($"{Tag} JoinGame: UnityTransport component not found!");
            return;
        }

        bool result = nm.StartClient();
        Debug.Log($"{Tag} StartClient result: {result}");
    }

    /// <summary>
    /// Disconnect from the current session.
    /// </summary>
    public void Disconnect()
    {
        Debug.Log($"{Tag} Disconnect called");
        NetworkManager.Singleton.Shutdown();
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        int connectedCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"{Tag} ApproveConnection: clientId={request.ClientNetworkId}, connected={connectedCount}/2");

        if (connectedCount >= 2)
        {
            response.Approved = false;
            response.Reason = "Server full (max 2 players)";
            Debug.LogWarning($"{Tag} Connection rejected: server full");
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;

        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            var spawnPoint = _spawnPoints[_nextSpawnIndex % _spawnPoints.Length];
            response.Position = spawnPoint.position;
            response.Rotation = spawnPoint.rotation;
            _pendingSpawns[request.ClientNetworkId] = (spawnPoint.position, spawnPoint.rotation);
            Debug.Log($"{Tag} Approved — spawn at {spawnPoint.position} (index {_nextSpawnIndex})");
            _nextSpawnIndex++;
        }
        else
        {
            Debug.LogWarning($"{Tag} Approved — no spawn points configured, using default position");
        }
    }
}

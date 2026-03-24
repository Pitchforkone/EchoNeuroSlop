using UnityEngine;
using Mirror;

/// <summary>
/// Network helper for synchronizing echo spawns across all clients.
/// This component must be on a GameObject with NetworkIdentity (e.g., on the NetworkManager or a dedicated network object).
/// </summary>
public class EchoNetworkHelper : NetworkBehaviour
{
    public static EchoNetworkHelper Instance { get; private set; }

    /// <summary>
    /// Event fired when echo should be spawned locally.
    /// EchoManager subscribes to this.
    /// </summary>
    public static event System.Action<Vector3, float, float, float, Color, float> OnNetworkEchoSpawn;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Request to spawn an echo. Will be synchronized to all clients.
    /// </summary>
    public void RequestSpawnEcho(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime)
    {
        if (isServer)
        {
            // Server directly broadcasts to all clients
            RpcSpawnEcho(position, speed, maxRadius, intensity, color, lifetime);
        }
        else
        {
            // Client sends command to server
            CmdSpawnEcho(position, speed, maxRadius, intensity, color, lifetime);
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdSpawnEcho(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime)
    {
        // Server received command, broadcast to all clients
        RpcSpawnEcho(position, speed, maxRadius, intensity, color, lifetime);
    }

    [ClientRpc]
    private void RpcSpawnEcho(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime)
    {
        // Fire event for local spawn
        OnNetworkEchoSpawn?.Invoke(position, speed, maxRadius, intensity, color, lifetime);
    }
}

using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Minimal connection UI: Host / Join buttons, IP input field.
/// Uses OnGUI for prototype simplicity — no Canvas needed.
/// Hides itself once connected.
/// </summary>
public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private GameNetworkManager _networkManager;

    private string _ipAddress = "127.0.0.1";
    private string _statusMessage = "";
    private bool _isConnecting;

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnGUI()
    {
        // Hide UI when connected and playing
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            return;

        var boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 16 };
        var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        var buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        var textFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 14 };

        float w = 300f;
        float h = 200f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "EchoNeuroSlop", boxStyle);

        float innerX = x + 20f;
        float innerW = w - 40f;
        float cy = y + 40f;

        GUI.Label(new Rect(innerX, cy, innerW, 25f), "IP Address:", labelStyle);
        cy += 28f;

        _ipAddress = GUI.TextField(new Rect(innerX, cy, innerW, 28f), _ipAddress, textFieldStyle);
        cy += 38f;

        GUI.enabled = !_isConnecting;

        if (GUI.Button(new Rect(innerX, cy, innerW / 2f - 5f, 35f), "Host", buttonStyle))
        {
            _statusMessage = "Starting host...";
            _isConnecting = true;
            Debug.Log("[UI] Host button pressed");
            _networkManager.StartHost();
        }

        if (GUI.Button(new Rect(innerX + innerW / 2f + 5f, cy, innerW / 2f - 5f, 35f), "Join", buttonStyle))
        {
            _statusMessage = $"Connecting to {_ipAddress}...";
            _isConnecting = true;
            Debug.Log($"[UI] Join button pressed, IP={_ipAddress}");
            _networkManager.JoinGame(_ipAddress);
        }

        GUI.enabled = true;
        cy += 45f;

        if (!string.IsNullOrEmpty(_statusMessage))
            GUI.Label(new Rect(innerX, cy, innerW, 25f), _statusMessage, labelStyle);
    }

    private void OnClientConnected(ulong clientId)
    {
        _isConnecting = false;
        if (NetworkManager.Singleton.IsHost)
        {
            int count = NetworkManager.Singleton.ConnectedClientsIds.Count;
            _statusMessage = $"Hosting — {count}/2 players";
        }
        else
        {
            _statusMessage = "Connected!";
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // If it's us disconnecting, show the UI again
        if (clientId == NetworkManager.Singleton.LocalClientId || !NetworkManager.Singleton.IsConnectedClient)
        {
            _isConnecting = false;
            _statusMessage = "Disconnected";
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Mirror;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// UI controller for Steam network operations.
/// Handles Host, Leave, Invite, Join and Copy Lobby ID buttons.
/// </summary>
public class SteamNetworkUI : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button leaveButton;
    public Button inviteButton;
    public Button joinButton;
    public Button copyLobbyIdButton;
    
    [Header("Input Fields")]
    public InputField lobbyIdInput;

    private SteamLobby steamLobby;

    private void Awake()
    {
        // Ensure cursor is visible for UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Fix EventSystem Input Module
        FixEventSystemInputModule();
    }

    private void FixEventSystemInputModule()
    {
        var eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogError("[SteamNetworkUI] No EventSystem found! Creating one...");
            var go = new GameObject("EventSystem");
            eventSystem = go.AddComponent<EventSystem>();
        }

        // Remove old Standalone Input Module if present
        var standaloneModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null)
        {
            Debug.Log("[SteamNetworkUI] Removing old StandaloneInputModule...");
            DestroyImmediate(standaloneModule);
        }

        // Add Input System UI Input Module if not present
        var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

    }

    private void Start()
    { 
        steamLobby = FindObjectOfType<SteamLobby>();
        
        if (steamLobby == null)
        {
            Debug.LogError("[SteamNetworkUI] SteamLobby NOT FOUND! Make sure SteamLobby component exists in the scene.");
        }

        // Setup buttons
        SetupButton(hostButton, "Host", OnHostClicked);
        SetupButton(leaveButton, "Leave", OnLeaveClicked);
        SetupButton(inviteButton, "Invite", OnInviteClicked);
        SetupButton(joinButton, "Join", OnJoinClicked);
        SetupButton(copyLobbyIdButton, "CopyLobbyId", CopyLobbyId);

        UpdateUI();
    }

    private void SetupButton(Button button, string name, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
        else
        {
            Debug.LogWarning($"[SteamNetworkUI] {name} button is not assigned");
        }
    }

    private void Update()
    {
        UpdateUI();
        
        // Keep cursor visible when not connected
        if (!NetworkClient.isConnected)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    private void UpdateUI()
    {
#if !DISABLESTEAMWORKS
        if (SteamLobby.CurrentLobbyID.IsValid())
        {
            if (copyLobbyIdButton != null)
                copyLobbyIdButton.gameObject.SetActive(true);
        }
        else
        {
            if (copyLobbyIdButton != null)
                copyLobbyIdButton.gameObject.SetActive(false);
        }
#endif

        if (hostButton != null)
            hostButton.interactable = !NetworkClient.isConnected && !NetworkServer.active;

        if (leaveButton != null)
            leaveButton.interactable = NetworkClient.isConnected || NetworkServer.active;

        if (inviteButton != null)
            inviteButton.interactable = NetworkServer.active;

        if (joinButton != null)
            joinButton.interactable = !NetworkClient.isConnected && !NetworkServer.active;
    }

    private void OnHostClicked()
    {
        
        if (steamLobby == null)
        {
            steamLobby = FindObjectOfType<SteamLobby>();
            if (steamLobby == null)
            {
                Debug.LogError("[SteamNetworkUI] Cannot find SteamLobby!");
                return;
            }
        }

        DestroyAllLights();
        steamLobby.HostLobby();
    }

    private void OnLeaveClicked()
    {
        Debug.Log("[SteamNetworkUI] >>> OnLeaveClicked <<<");
        if (steamLobby != null)
        {
            steamLobby.LeaveLobby();
        }
    }

    private void OnInviteClicked()
    {
        Debug.Log("[SteamNetworkUI] >>> OnInviteClicked <<<");
#if !DISABLESTEAMWORKS
        if (SteamManager.Initialized && SteamLobby.CurrentLobbyID.IsValid())
        {
            SteamFriends.ActivateGameOverlayInviteDialog(SteamLobby.CurrentLobbyID);
        }
        else
        {
            Debug.LogWarning("[SteamNetworkUI] Cannot invite - Steam not initialized or no active lobby!");
        }
#endif
    }

    private void OnJoinClicked()
    {
        Debug.Log("[SteamNetworkUI] >>> OnJoinClicked <<<");
#if !DISABLESTEAMWORKS
        if (steamLobby != null && lobbyIdInput != null && !string.IsNullOrEmpty(lobbyIdInput.text))
        {
            DestroyAllLights();
            steamLobby.JoinLobbyById(lobbyIdInput.text.Trim());
        }
        else
        {
            Debug.LogWarning("[SteamNetworkUI] Enter Lobby ID to join!");
        }
#endif
    }

    /// <summary>
    /// Finds all LightDestroy objects in the scene and destroys them.
    /// Called when hosting or joining a game.
    /// </summary>
    private void DestroyAllLights()
    {
        LightDestroy[] lights = FindObjectsOfType<LightDestroy>();
        Debug.Log($"[SteamNetworkUI] Found {lights.Length} LightDestroy objects to destroy");
        
        foreach (LightDestroy light in lights)
        {
            if (light != null)
            {
                light.DestroyLight();
            }
        }
    }

    public void CopyLobbyId()
    {
        Debug.Log("[SteamNetworkUI] >>> CopyLobbyId <<<");
#if !DISABLESTEAMWORKS
        if (SteamLobby.CurrentLobbyID.IsValid())
        {
            string lobbyId = SteamLobby.CurrentLobbyID.ToString();
            GUIUtility.systemCopyBuffer = lobbyId;
            Debug.Log($"[SteamNetworkUI] Lobby ID copied: {lobbyId}");
        }
#endif
    }
}

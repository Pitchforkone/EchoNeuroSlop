using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Steam Lobby manager for creating and joining lobbies via Steam.
/// Handles Steam callbacks and integrates with Photon PUN 2 networking.
/// </summary>
public class SteamLobby : MonoBehaviourPunCallbacks
{
#if !DISABLESTEAMWORKS
    // Callbacks
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> JoinRequest;
    protected Callback<LobbyEnter_t> LobbyEntered;

    // Lobby ID
    public static CSteamID CurrentLobbyID { get; private set; }

    [Header("Photon Settings")]
    [Tooltip("Maximum players in room")]
    public byte maxPlayers = 4;

    private const string HostAddressKey = "HostAddress";
    private const string PhotonRoomKey = "PhotonRoom";

    // Pending action to execute once Photon is connected
    private System.Action _pendingPhotonAction;
    private bool _photonReady;

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] SteamManager is not initialized!");
            return;
        }

        // Setup Steam callbacks
        LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        JoinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
        LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);

        // Connect to Photon master server
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("[SteamLobby] Connecting to Photon...");
        }
        else
        {
            _photonReady = true;
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[SteamLobby] Connected to Photon Master Server");
        _photonReady = true;

        // Execute any pending action (CreateRoom / JoinRoom)
        if (_pendingPhotonAction != null)
        {
            _pendingPhotonAction.Invoke();
            _pendingPhotonAction = null;
        }
    }

    /// <summary>
    /// Create a new Steam lobby and start hosting
    /// </summary>
    public void HostLobby()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Cannot create lobby - Steam not initialized!");
            return;
        }
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
    }

    /// <summary>
    /// Join a lobby by Steam ID (for manual joining)
    /// </summary>
    public void JoinLobbyById(ulong lobbyId)
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Cannot join lobby - Steam not initialized!");
            return;
        }

        Debug.Log($"[SteamLobby] Attempting to join lobby: {lobbyId}");
        SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
    }

    /// <summary>
    /// Join a lobby by Steam ID string
    /// </summary>
    public void JoinLobbyById(string lobbyIdString)
    {
        if (ulong.TryParse(lobbyIdString, out ulong lobbyId))
        {
            JoinLobbyById(lobbyId);
        }
        else
        {
            Debug.LogError($"[SteamLobby] Invalid lobby ID: {lobbyIdString}");
        }
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[SteamLobby] Failed to create lobby: {callback.m_eResult}");
            return;
        }

        CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        // Copy lobby ID to clipboard for easy sharing
        GUIUtility.systemCopyBuffer = CurrentLobbyID.ToString();

        string roomName = CurrentLobbyID.ToString();

        // Set lobby data so others can find the Photon room
        SteamMatchmaking.SetLobbyData(
            CurrentLobbyID,
            HostAddressKey,
            SteamUser.GetSteamID().ToString()
        );
        SteamMatchmaking.SetLobbyData(
            CurrentLobbyID,
            PhotonRoomKey,
            roomName
        );

        // Create Photon room (defer if not yet connected)
        ExecuteWhenPhotonReady(() =>
        {
            RoomOptions roomOptions = new RoomOptions { MaxPlayers = maxPlayers };
            PhotonNetwork.CreateRoom(roomName, roomOptions);
        });
    }

    private void OnJoinRequest(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        CurrentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        // If we're the host (MasterClient), we already created the room
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // Get Photon room name from lobby data
        string roomName = SteamMatchmaking.GetLobbyData(CurrentLobbyID, PhotonRoomKey);

        if (string.IsNullOrEmpty(roomName))
        {
            // Fallback: use lobby ID as room name
            roomName = CurrentLobbyID.ToString();
        }

        // Join Photon room (defer if not yet connected)
        ExecuteWhenPhotonReady(() =>
        {
            Debug.Log($"[SteamLobby] Joining Photon room: {roomName}");
            PhotonNetwork.JoinRoom(roomName);
        });
    }

    /// <summary>
    /// Executes an action immediately if Photon is ready, or queues it for OnConnectedToMaster.
    /// </summary>
    private void ExecuteWhenPhotonReady(System.Action action)
    {
        if (_photonReady && PhotonNetwork.IsConnectedAndReady)
        {
            action.Invoke();
        }
        else
        {
            Debug.Log("[SteamLobby] Photon not ready yet, queuing action...");
            _pendingPhotonAction = action;

            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
            }
        }
    }

    /// <summary>
    /// Leave the current lobby
    /// </summary>
    public void LeaveLobby()
    {
        if (CurrentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(CurrentLobbyID);
            CurrentLobbyID = CSteamID.Nil;
            Debug.Log("[SteamLobby] Left Steam lobby");
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
    }

    /// <summary>
    /// Get current lobby info for debugging
    /// </summary>
    public string GetLobbyInfo()
    {
        if (!CurrentLobbyID.IsValid())
            return "No active lobby";

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyID);
        string info = $"Lobby ID: {CurrentLobbyID}\nMembers: {memberCount}";
        
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyID, i);
            string memberName = SteamFriends.GetFriendPersonaName(memberId);
            info += $"\n  - {memberName} ({memberId})";
        }

        return info;
    }

    private void OnDestroy()
    {
        LeaveLobby();
    }

    private void OnApplicationQuit()
    {
        LeaveLobby();
    }

    private void OnGUI()
    {
        if (SteamManager.Initialized)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 200));
            GUILayout.BeginVertical("box");
            GUILayout.Label($"Steam: {SteamFriends.GetPersonaName()}");
            GUILayout.Label($"ID: {SteamUser.GetSteamID()}");
            if (CurrentLobbyID.IsValid())
            {
                GUILayout.Label($"Lobby: {CurrentLobbyID}");
                GUILayout.Label($"Players: {SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyID)}");
            }
            if (PhotonNetwork.InRoom)
            {
                GUILayout.Label($"Photon Room: {PhotonNetwork.CurrentRoom.Name}");
                GUILayout.Label($"Photon Players: {PhotonNetwork.CurrentRoom.PlayerCount}");
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
#else
    private void Start()
    {
        Debug.LogWarning("[SteamLobby] Steamworks is disabled!");
    }

    public void HostLobby()
    {
        Debug.LogWarning("[SteamLobby] Steamworks is disabled!");
    }

    public void LeaveLobby()
    {
        Debug.LogWarning("[SteamLobby] Steamworks is disabled!");
    }

    public void JoinLobbyById(ulong lobbyId)
    {
        Debug.LogWarning("[SteamLobby] Steamworks is disabled!");
    }

    public void JoinLobbyById(string lobbyIdString)
    {
        Debug.LogWarning("[SteamLobby] Steamworks is disabled!");
    }
#endif
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Mirror;
using TMPro;
using System.Text;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// UI controller for Steam network operations.
/// Handles Host, Leave, Invite, Join and Copy Lobby ID buttons.
/// Also displays voice hints when player enters voice activation zones.
/// Also displays inventory items with their voice keywords and counts.
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
    
    [Header("Menu Panel")]
    [Tooltip("Parent panel containing all menu buttons (will be toggled with ESC)")]
    public GameObject menuPanel;
    
    [Header("Voice Hint UI")]
    [Tooltip("Panel containing the voice hint (will be shown/hidden)")]
    public GameObject voiceHintPanel;
    [Tooltip("Text field to display the voice hint (e.g. 'Speak: Open')")]
    public TextMeshProUGUI voiceHintText;
    
    [Header("Inventory UI")]
    [Tooltip("Panel containing the inventory display (will be shown/hidden based on items)")]
    public GameObject inventoryPanel;
    [Tooltip("Text field to display inventory items with keywords and counts")]
    public TextMeshProUGUI inventoryText;

    private SteamLobby steamLobby;
    private PlayerInventory _currentInventory;
    private readonly StringBuilder _inventoryStringBuilder = new StringBuilder();
    
    private bool _menuVisible = true;
    private bool _wasConnected = false;

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

        // Hide voice hint by default
        HideVoiceHint();
        
        // Hide inventory panel by default
        HideInventoryPanel();
        
        // Show menu at start (before connecting)
        ShowMenu();

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
        
        // Toggle menu on ESC key (using new Input System)
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }
        
        // Check if just connected - hide menu automatically
        bool isConnected = NetworkClient.isConnected || NetworkServer.active;
        if (isConnected && !_wasConnected)
        {
            // Just connected - hide menu
            HideMenu();
        }
        else if (!isConnected && _wasConnected)
        {
            // Just disconnected - show menu
            ShowMenu();
        }
        _wasConnected = isConnected;
        
        // Cursor management
        if (_menuVisible || !isConnected)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else if (isConnected && !_menuVisible)
        {
            // Lock cursor when in game and menu is closed
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        // Check for PlayerInventory and subscribe to changes
        UpdateInventorySubscription();
    }

    /// <summary>
    /// Toggles the menu visibility.
    /// </summary>
    public void ToggleMenu()
    {
        if (_menuVisible)
        {
            HideMenu();
        }
        else
        {
            ShowMenu();
        }
    }
    
    /// <summary>
    /// Shows the menu panel.
    /// </summary>
    public void ShowMenu()
    {
        _menuVisible = true;
        
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }
        else
        {
            // Fallback: show individual elements if no panel assigned
            SetMenuElementsActive(true);
        }
        
        // Show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("[SteamNetworkUI] Menu shown");
    }
    
    /// <summary>
    /// Hides the menu panel.
    /// </summary>
    public void HideMenu()
    {
        _menuVisible = false;
        
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
        else
        {
            // Fallback: hide individual elements if no panel assigned
            SetMenuElementsActive(false);
        }
        
        Debug.Log("[SteamNetworkUI] Menu hidden");
    }
    
    /// <summary>
    /// Sets active state for all menu UI elements.
    /// Used as fallback when menuPanel is not assigned.
    /// </summary>
    private void SetMenuElementsActive(bool active)
    {
        if (hostButton != null) hostButton.gameObject.SetActive(active);
        if (leaveButton != null) leaveButton.gameObject.SetActive(active);
        if (inviteButton != null) inviteButton.gameObject.SetActive(active);
        if (joinButton != null) joinButton.gameObject.SetActive(active);
        if (copyLobbyIdButton != null) copyLobbyIdButton.gameObject.SetActive(active);
        if (lobbyIdInput != null) lobbyIdInput.gameObject.SetActive(active);
    }
    
    /// <summary>
    /// Returns true if menu is currently visible.
    /// </summary>
    public bool IsMenuVisible => _menuVisible;

    private void UpdateInventorySubscription()
    {
        var inventory = PlayerInventory.LocalInstance;
        
        // If inventory changed, update subscription
        if (inventory != _currentInventory)
        {
            // Unsubscribe from old inventory
            if (_currentInventory != null)
            {
                _currentInventory.OnInventoryChanged -= RefreshInventoryDisplay;
            }
            
            _currentInventory = inventory;
            
            // Subscribe to new inventory
            if (_currentInventory != null)
            {
                _currentInventory.OnInventoryChanged += RefreshInventoryDisplay;
                RefreshInventoryDisplay();
            }
            else
            {
                HideInventoryPanel();
            }
        }
    }

    /// <summary>
    /// Refreshes the inventory display UI.
    /// </summary>
    public void RefreshInventoryDisplay()
    {
        if (_currentInventory == null || _currentInventory.ItemCount == 0)
        {
            HideInventoryPanel();
            return;
        }
        
        _inventoryStringBuilder.Clear();
        _inventoryStringBuilder.AppendLine("<b>Inventory:</b>");
        
        foreach (var item in _currentInventory.Items)
        {
            // Format: "ItemName" - say "keyword" (x3)
            _inventoryStringBuilder.AppendLine($"• {item.DisplayName} - say \"<color=#FFD700>{item.Keyword}</color>\" (x{item.Count})");
        }
        
        if (inventoryText != null)
        {
            inventoryText.text = _inventoryStringBuilder.ToString();
        }
        
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Hides the inventory panel.
    /// </summary>
    public void HideInventoryPanel()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
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

    /// <summary>
    /// Shows the voice hint UI with the specified keyword.
    /// Called when player enters a VoiceActivateZone.
    /// </summary>
    /// <param name="keyword">The keyword to display.</param>
    public void ShowVoiceHint(string keyword)
    {
        if (voiceHintPanel != null)
        {
            voiceHintPanel.SetActive(true);
        }
        
        if (voiceHintText != null)
        {
            voiceHintText.text = $"Speak: \"{keyword}\"";
        }
        
        Debug.Log($"[SteamNetworkUI] Voice hint shown: {keyword}");
    }
    
    /// <summary>
    /// Hides the voice hint UI.
    /// Called when player exits a VoiceActivateZone.
    /// </summary>
    public void HideVoiceHint()
    {
        if (voiceHintPanel != null)
        {
            voiceHintPanel.SetActive(false);
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
    
    private void OnDestroy()
    {
        // Unsubscribe from inventory
        if (_currentInventory != null)
        {
            _currentInventory.OnInventoryChanged -= RefreshInventoryDisplay;
            _currentInventory = null;
        }
    }
}

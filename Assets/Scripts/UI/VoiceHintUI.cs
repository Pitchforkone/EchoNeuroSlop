using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Static helper class that displays voice hint when player enters a VoiceActivateZone.
/// Works with SteamNetworkUI to show/hide the hint panel.
/// </summary>
public static class VoiceHintUI
{
    private static SteamNetworkUI _cachedUI;
    
    /// <summary>
    /// Shows the voice hint with the specified keyword.
    /// Finds and uses SteamNetworkUI to display the hint.
    /// </summary>
    /// <param name="keyword">The keyword to display.</param>
    public static void Show(string keyword)
    {
        var ui = GetUI();
        if (ui != null)
        {
            ui.ShowVoiceHint(keyword);
        }
        else
        {
            Debug.LogWarning("[VoiceHintUI] No SteamNetworkUI found. Make sure it exists in the scene.");
        }
    }
    
    /// <summary>
    /// Hides the voice hint.
    /// </summary>
    public static void Hide()
    {
        var ui = GetUI();
        if (ui != null)
        {
            ui.HideVoiceHint();
        }
    }
    
    private static SteamNetworkUI GetUI()
    {
        if (_cachedUI == null)
        {
            _cachedUI = Object.FindObjectOfType<SteamNetworkUI>();
        }
        return _cachedUI;
    }
    
    /// <summary>
    /// Clears the cached UI reference. Call this when changing scenes.
    /// </summary>
    public static void ClearCache()
    {
        _cachedUI = null;
    }
}

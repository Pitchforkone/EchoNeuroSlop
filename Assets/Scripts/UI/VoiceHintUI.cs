using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Static helper that displays interaction hint when player enters an InteractZone.
/// Works with SteamNetworkUI to show/hide the hint panel.
/// </summary>
public static class InteractHintUI
{
    private static SteamNetworkUI _cachedUI;

    /// <summary>
    /// Shows the interaction hint with the specified text.
    /// </summary>
    /// <param name="hintText">The hint text to display.</param>
    public static void Show(string hintText)
    {
        var ui = GetUI();
        if (ui != null)
        {
            ui.ShowInteractHint(hintText);
        }
        else
        {
            Debug.LogWarning("[InteractHintUI] No SteamNetworkUI found. Make sure it exists in the scene.");
        }
    }

    /// <summary>
    /// Hides the interaction hint.
    /// </summary>
    public static void Hide()
    {
        var ui = GetUI();
        if (ui != null)
        {
            ui.HideInteractHint();
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

using System;
using UnityEngine;

/// <summary>
/// Ёлемент инвентар€ "Ёхо-граната".
/// »спользуетс€ дл€ броска гранаты голосовой командой "grenade".
/// </summary>
[Serializable]
public class GrenadeItem : BaseInventoryItem
{
    /// <summary>
    /// —обытие вызываетс€ когда игрок использует гранату.
    /// ѕараметр - количество оставшихс€ гранат.
    /// </summary>
    public static event Action<int> OnGrenadeUsed;
    
    public GrenadeItem(int count = 1) : base("grenade", "Echo Grenade", count)
    {
    }
    
    protected override void OnUse()
    {
        Debug.Log($"[GrenadeItem] Grenade used! Remaining: {_count}");
        
        // ”ведомл€ем подписчиков (VoiceGrenadeThrow)
        OnGrenadeUsed?.Invoke(_count);
    }
    
    protected override void OnAdded()
    {
        Debug.Log($"[GrenadeItem] Grenade added to inventory. Count: {_count}");
    }
    
    protected override void OnRemoved()
    {
        Debug.Log("[GrenadeItem] Grenade removed from inventory");
    }
}

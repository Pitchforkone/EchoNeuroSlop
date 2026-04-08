using System;
using UnityEngine;

/// <summary>
/// Ёлемент инвентар€ "ќчки ночного видени€".
/// </summary>
[Serializable]
public class NightVisionItem : BaseInventoryItem
{
    /// <summary>
    /// —обытие вызываетс€ когда игрок активирует очки ночного видени€.
    /// ѕараметр - количество оставшихс€ очков.
    /// </summary>
    public static event Action<int> OnNightVisionUsed;
    
    public NightVisionItem(int maxStack = 3, bool persistent = false) : base("Night Vision Goggles", 1, maxStack, persistent)
    {
    }
    
    protected override void OnUse()
    {
        Debug.Log($"[NightVisionItem] Night Vision activated! Remaining: {_count}");
        OnNightVisionUsed?.Invoke(_count);
    }
    
    protected override void OnAdded()
    {
        Debug.Log($"[NightVisionItem] Night Vision Goggles added to inventory. Count: {_count}");
    }
    
    protected override void OnRemoved()
    {
        Debug.Log("[NightVisionItem] Night Vision Goggles removed from inventory");
    }
}

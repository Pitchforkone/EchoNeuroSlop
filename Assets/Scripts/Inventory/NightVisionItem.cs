using System;
using UnityEngine;

/// <summary>
/// Элемент инвентаря "Очки ночного видения".
/// Используется для активации ночного видения голосовой командой "glasses".
/// </summary>
[Serializable]
public class NightVisionItem : BaseInventoryItem
{
    /// <summary>
    /// Событие вызывается когда игрок активирует очки ночного видения.
    /// Параметр - количество оставшихся очков.
    /// </summary>
    public static event Action<int> OnNightVisionUsed;
    
    public NightVisionItem(int count = 1) : base("glasses", "Night Vision Goggles", count)
    {
    }
    
    protected override void OnUse()
    {
        Debug.Log($"[NightVisionItem] Night Vision activated! Remaining: {_count}");
        
        // Уведомляем подписчиков (NightVisionController)
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

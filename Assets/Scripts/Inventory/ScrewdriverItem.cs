using System;
using UnityEngine;

/// <summary>
/// Элемент инвентаря "Отвёртка от вентиляции".
/// Используется для откручивания вентиляционных решёток.
/// </summary>
[Serializable]
public class ScrewdriverItem : BaseInventoryItem
{
    /// <summary>
    /// Событие вызывается когда игрок использует отвёртку.
    /// </summary>
    public static event Action OnScrewdriverUsed;
    public override bool CanUseManually => false;

    public ScrewdriverItem(int maxStack = 1, bool persistent = false) : base("Screwdriver", 1, maxStack, persistent)
    {
    }

    protected override void OnUse()
    {
        Debug.Log("[ScrewdriverItem] Screwdriver used!");
        OnScrewdriverUsed?.Invoke();
    }

    protected override void OnAdded()
    {
        Debug.Log($"[ScrewdriverItem] Screwdriver added to inventory. Count: {_count}");
    }

    protected override void OnRemoved()
    {
        Debug.Log("[ScrewdriverItem] Screwdriver removed from inventory");
    }
}

using UnityEngine;

/// <summary>
/// Элемент инвентаря "Ключ от выхода".
/// </summary>
public class ExitKeyItem : BaseInventoryItem
{
    public override bool CanUseManually => false;
    
    public ExitKeyItem(int maxStack = 1, bool persistent = false) : base("Exit Key", 1, maxStack, persistent)
    {
    }
    
    protected override void OnUse()
    {
    }
    
    protected override void OnAdded()
    {
    }
    
    protected override void OnRemoved()
    {
    }
}

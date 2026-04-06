using UnityEngine;

/// <summary>
/// Элемент инвентаря "Ключ от выхода".
/// </summary>
public class ExitKeyItem : BaseInventoryItem
{
    public override bool CanUseManually => false;
    
    public ExitKeyItem(int count = 1) : base("Exit Key", count, 1)
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

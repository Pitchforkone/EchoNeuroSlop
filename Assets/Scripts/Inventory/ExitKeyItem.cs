using UnityEngine;

/// <summary>
/// Предмет инвентаря "Ключ от выхода".
/// Используется для открытия двери выхода голосовой командой "key".
/// </summary>
public class ExitKeyItem : BaseInventoryItem
{
    public ExitKeyItem(int count = 1) : base("key", "Exit Key", count)
    {
    }
    
    protected override void OnUse()
    {
        Debug.Log("[ExitKeyItem] Exit Key used!");
        // Логика использования ключа будет обрабатываться в двери выхода,
        // которая слушает голосовую команду "key" и проверяет наличие ключа в инвентаре
    }
    
    protected override void OnAdded()
    {
        Debug.Log("[ExitKeyItem] Exit Key added to inventory");
    }
    
    protected override void OnRemoved()
    {
        Debug.Log("[ExitKeyItem] Exit Key removed from inventory");
    }
}

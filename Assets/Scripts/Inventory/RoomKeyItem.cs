using UnityEngine;

/// <summary>
/// ÷ветной ключ от комнаты. ќткрывает двери соответствующего цвета.
/// </summary>
public class RoomKeyItem : BaseInventoryItem
{
    public override bool CanUseManually => false;

    private KeyColor _keyColor;

    public KeyColor KeyColor => _keyColor;

    public RoomKeyItem(KeyColor keyColor, int maxStack = 1, bool persistent = false) 
        : base(GetKeyName(keyColor), 1, maxStack, persistent)
    {
        _keyColor = keyColor;
    }

    private static string GetKeyName(KeyColor color)
    {
        return $"{color} Key";
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

using UnityEngine;

/// <summary>
/// Интерфейс для предметов инвентаря.
/// </summary>
public interface IInventoryItem
{
    /// <summary>
    /// Отображаемое имя предмета.
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Текущее количество экземпляров предмета.
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// Максимальное количество предметов в одном слоте.
    /// </summary>
    int MaxStack { get; }
    
    /// <summary>
    /// Можно ли использовать предмет вручную (нажатием F).
    /// Если false — предмет расходуется только через игровую логику (например, при открытии двери).
    /// </summary>
    bool CanUseManually { get; }
    
    /// <summary>
    /// Использовать предмет. Уменьшает Count на 1.
    /// </summary>
    /// <returns>True если предмет был успешно использован.</returns>
    bool Use();
    
    /// <summary>
    /// Добавить количество к предмету.
    /// </summary>
    /// <param name="amount">Количество для добавления.</param>
    /// <returns>Количество, которое не поместилось (overflow).</returns>
    int AddCount(int amount);
    
    /// <summary>
    /// Вызывается когда предмет добавляется в инвентарь.
    /// </summary>
    void OnAddedToInventory(PlayerInventory inventory);
    
    /// <summary>
    /// Вызывается когда предмет удаляется из инвентаря.
    /// </summary>
    void OnRemovedFromInventory();
}

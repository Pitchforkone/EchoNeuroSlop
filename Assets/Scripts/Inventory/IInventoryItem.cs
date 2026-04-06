using UnityEngine;

/// <summary>
/// Интерфейс для предметов инвентаря.
/// Каждый предмет имеет ключевое слово для отображения и возможность использования.
/// </summary>
public interface IInventoryItem
{
    /// <summary>
    /// Ключевое слово для отображения предмета.
    /// </summary>
    string Keyword { get; }
    
    /// <summary>
    /// Отображаемое имя предмета.
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Текущее количество экземпляров предмета.
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// Использовать предмет. Уменьшает Count на 1.
    /// </summary>
    /// <returns>True если предмет был успешно использован.</returns>
    bool Use();
    
    /// <summary>
    /// Добавить количество к предмету.
    /// </summary>
    /// <param name="amount">Количество для добавления.</param>
    void AddCount(int amount);
    
    /// <summary>
    /// Вызывается когда предмет добавляется в инвентарь.
    /// </summary>
    /// <param name="inventory">Инвентарь, в который добавлен предмет.</param>
    void OnAddedToInventory(PlayerInventory inventory);
    
    /// <summary>
    /// Вызывается когда предмет удаляется из инвентаря.
    /// </summary>
    void OnRemovedFromInventory();
}

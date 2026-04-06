using System;
using UnityEngine;

/// <summary>
/// Базовый класс для предметов инвентаря.
/// Наследуйте от этого класса для создания конкретных предметов.
/// </summary>
[Serializable]
public abstract class BaseInventoryItem : IInventoryItem
{
    [SerializeField] protected string _keyword;
    [SerializeField] protected string _displayName;
    [SerializeField] protected int _count = 1;
    
    protected PlayerInventory _inventory;
    
    public string Keyword => _keyword;
    public string DisplayName => _displayName;
    public int Count => _count;
    
    protected BaseInventoryItem(string keyword, string displayName, int count = 1)
    {
        _keyword = keyword.ToLowerInvariant();
        _displayName = displayName;
        _count = Mathf.Max(1, count);
    }
    
    public virtual bool Use()
    {
        if (_count <= 0) return false;
        
        _count--;
        OnUse();
        
        return true;
    }
    
    public void AddCount(int amount)
    {
        if (amount > 0)
        {
            _count += amount;
        }
    }
    
    public void OnAddedToInventory(PlayerInventory inventory)
    {
        _inventory = inventory;
        OnAdded();
    }
    
    public void OnRemovedFromInventory()
    {
        OnRemoved();
        _inventory = null;
    }
    
    /// <summary>
    /// Переопределите для выполнения действия при использовании предмета.
    /// </summary>
    protected abstract void OnUse();
    
    /// <summary>
    /// Переопределите для выполнения действия при добавлении в инвентарь.
    /// </summary>
    protected virtual void OnAdded() { }
    
    /// <summary>
    /// Переопределите для выполнения действия при удалении из инвентаря.
    /// </summary>
    protected virtual void OnRemoved() { }
}

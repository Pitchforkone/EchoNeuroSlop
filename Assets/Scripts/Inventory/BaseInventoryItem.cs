using System;
using UnityEngine;

/// <summary>
/// Базовый класс для предметов инвентаря.
/// Наследуйте от этого класса для создания конкретных предметов.
/// </summary>
[Serializable]
public abstract class BaseInventoryItem : IInventoryItem
{
    [SerializeField] protected string _displayName;
    [SerializeField] protected int _count = 1;
    [SerializeField] protected int _maxStack = 99;
    
    protected PlayerInventory _inventory;
    
    public string DisplayName => _displayName;
    public int Count => _count;
    public int MaxStack => _maxStack;
    public virtual bool CanUseManually => true;
    
    protected BaseInventoryItem(string displayName, int count = 1, int maxStack = 99)
    {
        _displayName = displayName;
        _count = Mathf.Max(1, count);
        _maxStack = Mathf.Max(1, maxStack);
    }
    
    public virtual bool Use()
    {
        if (_count <= 0) return false;
        
        _count--;
        OnUse();
        
        return true;
    }
    
    /// <summary>
    /// Добавить количество к предмету с учётом MaxStack.
    /// </summary>
    /// <returns>Количество, которое не поместилось (overflow).</returns>
    public int AddCount(int amount)
    {
        if (amount <= 0) return 0;
        
        int space = _maxStack - _count;
        if (amount <= space)
        {
            _count += amount;
            return 0;
        }
        else
        {
            _count = _maxStack;
            return amount - space;
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

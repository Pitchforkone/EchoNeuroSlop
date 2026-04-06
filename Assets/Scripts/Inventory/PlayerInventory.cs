using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Инвентарь локального игрока.
/// Хранит предметы и управляет их жизненным циклом.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    /// <summary>
    /// Ссылка на инвентарь локального игрока.
    /// </summary>
    public static PlayerInventory LocalInstance { get; private set; }
    
    /// <summary>
    /// Событие вызывается при изменении инвентаря (добавление/удаление/изменение количества).
    /// </summary>
    public event Action OnInventoryChanged;
    
    private readonly List<IInventoryItem> _items = new List<IInventoryItem>();
    
    /// <summary>
    /// Доступ к списку предметов инвентаря (только для чтения).
    /// </summary>
    public IReadOnlyList<IInventoryItem> Items => _items.AsReadOnly();
    
    /// <summary>
    /// Количество предметов в инвентаре.
    /// </summary>
    public int ItemCount => _items.Count;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        InitializeAsLocal();
    }

    private void Start()
    {
        if (!NetworkClient.active)
        {
            InitializeAsLocal();
        }
    }

    private void InitializeAsLocal()
    {
        if (LocalInstance != null && LocalInstance != this)
        {
            Debug.LogWarning("[PlayerInventory] Replacing existing LocalInstance");
        }
        
        LocalInstance = this;
    }

    /// <summary>
    /// Добавить предмет в инвентарь.
    /// Если предмет с таким ключевым словом уже есть, увеличивается количество.
    /// </summary>
    public void AddItem(IInventoryItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerInventory] Attempted to add null item");
            return;
        }
        
        var existingItem = FindItemByKeyword(item.Keyword);
        if (existingItem != null)
        {
            existingItem.AddCount(item.Count);
            Debug.Log($"[PlayerInventory] Increased count of '{item.DisplayName}' to {existingItem.Count}");
        }
        else
        {
            _items.Add(item);
            item.OnAddedToInventory(this);
        }
        
        NotifyInventoryChanged();
    }

    /// <summary>
    /// Удалить предмет из инвентаря.
    /// </summary>
    public bool RemoveItem(IInventoryItem item)
    {
        if (item == null) return false;
        
        if (_items.Remove(item))
        {
            item.OnRemovedFromInventory();
            
            Debug.Log($"[PlayerInventory] Removed item '{item.DisplayName}'");
            NotifyInventoryChanged();
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Найти предмет по ключевому слову.
    /// </summary>
    public IInventoryItem FindItemByKeyword(string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return null;
        
        string lowerKeyword = keyword.ToLowerInvariant();
        foreach (var item in _items)
        {
            if (item.Keyword.ToLowerInvariant() == lowerKeyword)
                return item;
        }
        
        return null;
    }

    /// <summary>
    /// Использовать предмет. Если количество достигает 0, предмет удаляется.
    /// </summary>
    public bool UseItem(IInventoryItem item)
    {
        if (item == null || !_items.Contains(item)) return false;
        
        if (item.Use())
        {
            Debug.Log($"[PlayerInventory] Used item '{item.DisplayName}', remaining: {item.Count}");
            
            if (item.Count <= 0)
            {
                RemoveItem(item);
            }
            else
            {
                NotifyInventoryChanged();
            }
            
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Уведомить UI об изменении инвентаря.
    /// </summary>
    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        CleanupLocal();
    }

    private void OnDisable()
    {
        if (!NetworkClient.active)
        {
            CleanupLocal();
        }
    }

    private void OnDestroy()
    {
        CleanupLocal();
    }

    private void CleanupLocal()
    {
        foreach (var item in _items)
        {
            item.OnRemovedFromInventory();
        }
        _items.Clear();
        
        if (LocalInstance == this)
        {
            LocalInstance = null;
        }
        
        Debug.Log("[PlayerInventory] Cleaned up");
    }
}

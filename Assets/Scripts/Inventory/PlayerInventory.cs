using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Компонент инвентаря игрока.
/// Хранит предметы и управляет их подпиской на VoiceRecognizer.
/// Работает только для локального игрока.
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
    /// Получить копию списка предметов (только для чтения).
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
        // Для синглплеера
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
    /// Если предмет с таким ключевым словом уже есть, увеличивает количество.
    /// </summary>
    /// <param name="item">Предмет для добавления.</param>
    public void AddItem(IInventoryItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerInventory] Attempted to add null item");
            return;
        }
        
        // Проверяем, есть ли уже предмет с таким ключевым словом
        var existingItem = FindItemByKeyword(item.Keyword);
        if (existingItem != null)
        {
            // Увеличиваем количество существующего предмета
            existingItem.AddCount(item.Count);
            Debug.Log($"[PlayerInventory] Increased count of '{item.DisplayName}' to {existingItem.Count}");
        }
        else
        {
            // Добавляем новый предмет
            _items.Add(item);
            item.OnAddedToInventory(this);
            
            // Подписываем на VoiceRecognizer
            RegisterItemToVoiceRecognizer(item);
            
        }
        
        NotifyInventoryChanged();
    }

    /// <summary>
    /// Удалить предмет из инвентаря.
    /// </summary>
    /// <param name="item">Предмет для удаления.</param>
    /// <returns>True если предмет был найден и удален.</returns>
    public bool RemoveItem(IInventoryItem item)
    {
        if (item == null) return false;
        
        if (_items.Remove(item))
        {
            UnregisterItemFromVoiceRecognizer(item);
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
    /// <param name="keyword">Ключевое слово (регистронезависимо).</param>
    /// <returns>Найденный предмет или null.</returns>
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
    /// Использовать предмет. Если количество достигло 0, предмет удаляется.
    /// </summary>
    /// <param name="item">Предмет для использования.</param>
    /// <returns>True если предмет был успешно использован.</returns>
    public bool UseItem(IInventoryItem item)
    {
        if (item == null || !_items.Contains(item)) return false;
        
        if (item.Use())
        {
            Debug.Log($"[PlayerInventory] Used item '{item.DisplayName}', remaining: {item.Count}");
            
            // Если количество достигло 0, удаляем предмет
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

    private void RegisterItemToVoiceRecognizer(IInventoryItem item)
    {
        if (VoiceRecognizer.LocalInstance != null)
        {
            VoiceRecognizer.LocalInstance.AddListener(item);
            //Debug.Log($"[PlayerInventory] Registered '{item.DisplayName}' to VoiceRecognizer");
        }
        else
        {
            Debug.LogError("[PlayerInventory] VoiceRecognizer.LocalInstance is null, cannot register item");
        }
    }

    private void UnregisterItemFromVoiceRecognizer(IInventoryItem item)
    {
        if (VoiceRecognizer.LocalInstance != null)
        {
            VoiceRecognizer.LocalInstance.RemoveListener(item);
            Debug.Log($"[PlayerInventory] Unregistered '{item.DisplayName}' from VoiceRecognizer");
        }
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        CleanupLocal();
    }

    private void OnDisable()
    {
        // Для синглплеера
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
        // Отписываем все предметы от VoiceRecognizer
        foreach (var item in _items)
        {
            UnregisterItemFromVoiceRecognizer(item);
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

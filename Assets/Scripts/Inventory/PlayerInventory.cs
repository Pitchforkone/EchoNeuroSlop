using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Компонент инвентаря игрока.
/// Работает локально на клиенте.
/// </summary>
public class PlayerInventory : MonoBehaviourPun
{
    public static PlayerInventory LocalInstance { get; private set; }
    
    public event Action OnInventoryChanged;
    
    private readonly List<IInventoryItem> _items = new List<IInventoryItem>();
    
    public IReadOnlyList<IInventoryItem> Items => _items.AsReadOnly();
    public int ItemCount => _items.Count;

    private void Start()
    {
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
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
            
            RegisterItemToVoiceRecognizer(item);
        }
        
        NotifyInventoryChanged();
    }

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

    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private void RegisterItemToVoiceRecognizer(IInventoryItem item)
    {
        if (VoiceRecognizer.LocalInstance != null)
        {
            VoiceRecognizer.LocalInstance.AddListener(item);
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

    private void OnDisable()
    {
        if (!PhotonNetwork.IsConnected)
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

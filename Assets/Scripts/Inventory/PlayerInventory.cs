using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// Инвентарь локального игрока с 3 слотами.
/// Каждый слот может хранить один тип предмета с ограничением по MaxStack.
/// Клавиши 1/2/3 — переключение слотов, F — использование выбранного слота.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    public const int SlotCount = 3;
    
    /// <summary>
    /// Ссылка на инвентарь локального игрока.
    /// </summary>
    public static PlayerInventory LocalInstance { get; private set; }
    
    /// <summary>
    /// Событие вызывается при изменении инвентаря (добавление/удаление/изменение количества/смена слота).
    /// </summary>
    public event Action OnInventoryChanged;
    
    private readonly IInventoryItem[] _slots = new IInventoryItem[SlotCount];
    
    private int _selectedSlot = 0;
    
    /// <summary>
    /// Текущий выбранный слот (0-2).
    /// </summary>
    public int SelectedSlot => _selectedSlot;
    
    /// <summary>
    /// Доступ к слотам инвентаря (только для чтения).
    /// </summary>
    public IInventoryItem[] Slots => _slots;
    
    /// <summary>
    /// Доступ к списку предметов инвентаря (непустые слоты, для совместимости).
    /// </summary>
    public IReadOnlyList<IInventoryItem> Items
    {
        get
        {
            var list = new List<IInventoryItem>();
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] != null)
                    list.Add(_slots[i]);
            }
            return list.AsReadOnly();
        }
    }
    
    /// <summary>
    /// Количество занятых слотов.
    /// </summary>
    public int ItemCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] != null) count++;
            }
            return count;
        }
    }

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
    
    private void Update()
    {
        if (LocalInstance != this) return;
        
        HandleInput();
    }
    
    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        // Переключение слотов клавишами 1, 2, 3
        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            SelectSlot(0);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            SelectSlot(1);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            SelectSlot(2);
        }
        
        // Использование предмета клавишей F
        if (keyboard.fKey.wasPressedThisFrame)
        {
            UseSelectedSlot();
        }
    }
    
    /// <summary>
    /// Выбрать слот (0-2).
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;
        
        _selectedSlot = slotIndex;
        Debug.Log($"[PlayerInventory] Selected slot {slotIndex + 1}");
        NotifyInventoryChanged();
    }
    
    /// <summary>
    /// Использовать предмет в выбранном слоте.
    /// </summary>
    public void UseSelectedSlot()
    {
        var item = _slots[_selectedSlot];
        if (item == null)
        {
            Debug.Log($"[PlayerInventory] Slot {_selectedSlot + 1} is empty");
            return;
        }
        
        if (!item.CanUseManually)
        {
            Debug.Log($"[PlayerInventory] '{item.DisplayName}' cannot be used manually");
            return;
        }
        
        UseItem(item);
    }

    /// <summary>
    /// Добавить предмет в инвентарь.
    /// Сначала пытается стакнуть в существующий слот с тем же типом,
    /// затем занимает пустой слот. Возвращает true если удалось добавить.
    /// </summary>
    public bool AddItem(IInventoryItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("[PlayerInventory] Attempted to add null item");
            return false;
        }
        
        int remaining = item.Count;
        
        // 1. Пытаемся стакнуть в существующий слот с тем же типом предмета
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] != null && _slots[i].DisplayName == item.DisplayName)
            {
                remaining = _slots[i].AddCount(remaining);
                if (remaining <= 0)
                {
                    Debug.Log($"[PlayerInventory] Stacked '{item.DisplayName}' into slot {i + 1}, total: {_slots[i].Count}");
                    NotifyInventoryChanged();
                    return true;
                }
            }
        }
        
        // 2. Занимаем первый пустой слот
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = item;
                item.OnAddedToInventory(this);
                Debug.Log($"[PlayerInventory] Added '{item.DisplayName}' to slot {i + 1}, count: {item.Count}");
                NotifyInventoryChanged();
                return true;
            }
        }
        
        Debug.LogWarning($"[PlayerInventory] Inventory full! Cannot add '{item.DisplayName}'");
        return false;
    }

    /// <summary>
    /// Удалить предмет из слота.
    /// </summary>
    public bool RemoveItem(IInventoryItem item)
    {
        if (item == null) return false;
        
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] == item)
            {
                _slots[i].OnRemovedFromInventory();
                _slots[i] = null;
                Debug.Log($"[PlayerInventory] Removed item from slot {i + 1}");
                NotifyInventoryChanged();
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Использовать предмет. Если количество достигает 0, слот очищается.
    /// </summary>
    public bool UseItem(IInventoryItem item)
    {
        if (item == null) return false;
        
        // Найти слот с этим предметом
        int slotIndex = -1;
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] == item)
            {
                slotIndex = i;
                break;
            }
        }
        
        if (slotIndex < 0) return false;
        
        if (item.Use())
        {
            Debug.Log($"[PlayerInventory] Used item '{item.DisplayName}' in slot {slotIndex + 1}, remaining: {item.Count}");
            
            if (item.Count <= 0)
            {
                _slots[slotIndex].OnRemovedFromInventory();
                _slots[slotIndex] = null;
                Debug.Log($"[PlayerInventory] Slot {slotIndex + 1} emptied");
            }
            
            NotifyInventoryChanged();
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Получить предмет в конкретном слоте.
    /// </summary>
    public IInventoryItem GetSlot(int index)
    {
        if (index < 0 || index >= SlotCount) return null;
        return _slots[index];
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
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] != null)
            {
                _slots[i].OnRemovedFromInventory();
                _slots[i] = null;
            }
        }
        
        if (LocalInstance == this)
        {
            LocalInstance = null;
        }
    }
}

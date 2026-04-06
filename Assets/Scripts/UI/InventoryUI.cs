using UnityEngine;

/// <summary>
/// Главный скрипт UI инвентаря. Вешается на корневой Canvas-префаб.
/// На старте собирает все дочерние InventorySlotUI, назначает им индексы,
/// подписывается на PlayerInventory.LocalInstance и обновляет слоты.
///
/// Структура префаба:
///   InventoryCanvas (Canvas + InventoryUI)
///     └─ SlotsContainer
///         ├─ Slot_0 (InventorySlotUI)
///         ├─ Slot_1 (InventorySlotUI)
///         └─ Slot_2 (InventorySlotUI)
///
/// Mirror: каждый клиент имеет свой Canvas в сцене.
/// InventoryUI автоматически привязывается к PlayerInventory.LocalInstance,
/// поэтому отображает инвентарь только локального игрока.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("Если не задано — будут найдены автоматически среди дочерних объектов")]
    [SerializeField] private InventorySlotUI[] slots;

    private PlayerInventory _inventory;
    private bool _initialized;

    private void Start()
    {
        CollectSlots();
        ClearAllSlots();
    }

    private void Update()
    {
        BindToLocalInventory();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // ──────────────────────────────────────────────
    // Инициализация
    // ──────────────────────────────────────────────

    /// <summary>
    /// Собирает InventorySlotUI из дочерних объектов, если массив не задан вручную.
    /// Назначает каждому слоту индекс и подсказку клавиши.
    /// </summary>
    private void CollectSlots()
    {
        if (slots == null || slots.Length == 0)
        {
            slots = GetComponentsInChildren<InventorySlotUI>(true);
        }

        if (slots.Length != PlayerInventory.SlotCount)
        {
            Debug.LogWarning(
                $"[InventoryUI] Ожидалось {PlayerInventory.SlotCount} слотов, " +
                $"найдено {slots.Length}. Лишние слоты будут проигнорированы.");
        }

        for (int i = 0; i < slots.Length && i < PlayerInventory.SlotCount; i++)
        {
            slots[i].SlotIndex = i;
        }

        _initialized = true;
    }

    // ──────────────────────────────────────────────
    // Привязка к инвентарю локального игрока
    // ──────────────────────────────────────────────

    /// <summary>
    /// Каждый кадр проверяет, не сменился ли LocalInstance.
    /// Если сменился — переподписывается.
    /// </summary>
    private void BindToLocalInventory()
    {
        var local = PlayerInventory.LocalInstance;

        if (local == _inventory) return;

        Unsubscribe();

        _inventory = local;

        if (_inventory != null)
        {
            _inventory.OnInventoryChanged += RefreshAllSlots;
            RefreshAllSlots();
        }
        else
        {
            ClearAllSlots();
        }
    }

    private void Unsubscribe()
    {
        if (_inventory != null)
        {
            _inventory.OnInventoryChanged -= RefreshAllSlots;
            _inventory = null;
        }
    }

    // ──────────────────────────────────────────────
    // Обновление UI
    // ──────────────────────────────────────────────

    /// <summary>
    /// Обновляет визуал всех слотов по данным текущего инвентаря.
    /// </summary>
    private void RefreshAllSlots()
    {
        if (!_initialized || slots == null) return;

        int count = Mathf.Min(slots.Length, PlayerInventory.SlotCount);

        for (int i = 0; i < count; i++)
        {
            if (slots[i] == null) continue;

            var item = _inventory != null ? _inventory.GetSlot(i) : null;
            bool selected = _inventory != null && _inventory.SelectedSlot == i;

            slots[i].UpdateVisual(item, selected);
        }
    }

    /// <summary>
    /// Очищает все слоты (пустые, невыбранные).
    /// </summary>
    private void ClearAllSlots()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].Clear();
        }
    }
}

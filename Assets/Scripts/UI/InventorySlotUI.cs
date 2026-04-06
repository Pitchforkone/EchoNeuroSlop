using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Компонент одного слота инвентаря.
/// Вешается на каждый дочерний объект-слот внутри Canvas-префаба.
/// InventoryUI находит все слоты на старте и управляет ими.
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Фон слота (Image, меняет цвет при выделении)")]
    [SerializeField] private Image background;
    
    [Tooltip("Рамка слота (Image, меняет цвет при выделении)")]
    [SerializeField] private Image border;
    
    [Tooltip("Название предмета")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    
    [Tooltip("Количество предметов (x3)")]
    [SerializeField] private TextMeshProUGUI countText;
    
    [Tooltip("Подсказка 'Press F' — показывается только у выбранного непустого слота")]
    [SerializeField] private GameObject usePressHint;

    [Header("Цвета")]
    [SerializeField] private Color normalBgColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
    [SerializeField] private Color selectedBgColor = new Color(0.25f, 0.22f, 0.08f, 0.9f);
    [SerializeField] private Color normalBorderColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color selectedBorderColor = new Color(1f, 0.84f, 0f, 1f);
    [SerializeField] private Color emptyNameColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color filledNameColor = Color.white;
    [SerializeField] private Color countColor = new Color(1f, 0.84f, 0f, 1f);

    /// <summary>
    /// Индекс слота (0-2). Устанавливается из InventoryUI при инициализации.
    /// </summary>
    public int SlotIndex { get; set; }

    /// <summary>
    /// Обновляет визуал слота по данным предмета и состоянию выделения.
    /// </summary>
    public void UpdateVisual(IInventoryItem item, bool isSelected)
    {
        // Рамка и фон
        if (border != null)
            border.color = isSelected ? selectedBorderColor : normalBorderColor;

        if (background != null)
            background.color = isSelected ? selectedBgColor : normalBgColor;

        if (item != null)
        {
            // Слот занят
            if (itemNameText != null)
            {
                itemNameText.text = item.DisplayName;
                itemNameText.color = filledNameColor;
            }

            if (countText != null)
                countText.text = item.Count > 1 ? $"x{item.Count}" : "";
        }
        else
        {
            // Слот пустой
            if (itemNameText != null)
            {
                itemNameText.text = "Empty";
                itemNameText.color = emptyNameColor;
            }

            if (countText != null)
                countText.text = "";
        }

        // Подсказка использования — только выбранный непустой слот
        if (usePressHint != null)
            usePressHint.SetActive(isSelected && item != null);
    }

    /// <summary>
    /// Очищает визуал слота (пустой, невыбранный).
    /// </summary>
    public void Clear()
    {
        UpdateVisual(null, false);
    }
}

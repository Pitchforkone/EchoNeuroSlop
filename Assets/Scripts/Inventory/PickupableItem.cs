using UnityEngine;
using Mirror;

/// <summary>
/// Компонент для предметов на сцене, которые можно подобрать нажатием E.
/// Использует InteractZone для определения близости игрока.
/// При нажатии E предмет добавляется в инвентарь и уничтожается на сервере.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PickupableItem : NetworkBehaviour, IInteractable
{
    [Header("Interact Zone")]
    [Tooltip("Ссылка на дочерний объект с InteractZone")]
    public InteractZone interactZone;
    
    [Header("Item Settings")]
    [Tooltip("Тип предмета, который будет добавлен в инвентарь")]
    [SerializeField] private PickupItemType _itemType = PickupItemType.ExitKey;

    [Tooltip("Максимальное количество предметов в одном стаке")]
    [SerializeField] private int _maxStack = 1;

    [Tooltip("Предмет не пропадает после использования")]
    [SerializeField] private bool _persistent = false;

    [Tooltip("Цвет ключа (только для RoomKey)")]
    [SerializeField] private KeyColor _keyColor = KeyColor.Red;

    [SyncVar]
    private bool _isPickedUp = false;
    
    public enum PickupItemType
    {
        ExitKey,
        Grenade,
        NightVision,
        Screwdriver,
        RoomKey
    }
    
    private void Start()
    {
        if (interactZone == null)
        {
            interactZone = GetComponentInChildren<InteractZone>();
        }

        if (interactZone == null)
        {
            Debug.LogError("[PickupableItem] InteractZone not assigned and not found in children!");
            return;
        }

        interactZone.SetHintText("Take");
        interactZone.interact += OnInteract;
    }
    
    private void OnDestroy()
    {
        if (interactZone != null)
        {
            interactZone.interact -= OnInteract;
        }
    }

    /// <summary>
    /// Вызывается при нажатии E в зоне.
    /// </summary>
    public void OnInteract()
    {
        if (_isPickedUp) return;
        
        // Добавляем предмет в инвентарь локально
        AddItemToLocalInventory();
        
        // Отправляем команду на сервер для удаления объекта
        CmdPickupItem();
    }
    
    private void AddItemToLocalInventory()
    {
        if (PlayerInventory.LocalInstance == null)
        {
            Debug.LogError("[PickupableItem] PlayerInventory.LocalInstance is null!");
            return;
        }
        
        IInventoryItem item = CreateItem();
        if (item != null)
        {
            PlayerInventory.LocalInstance.AddItem(item);
        }
    }
    
    private IInventoryItem CreateItem()
    {
        switch (_itemType)
        {
            case PickupItemType.ExitKey:
                return new ExitKeyItem(_maxStack, _persistent);
            case PickupItemType.Grenade:
                return new GrenadeItem(_maxStack, _persistent);
            case PickupItemType.NightVision:
                return new NightVisionItem(_maxStack, _persistent);
            case PickupItemType.Screwdriver:
                return new ScrewdriverItem(_maxStack, _persistent);
            case PickupItemType.RoomKey:
                return new RoomKeyItem(_keyColor, _maxStack, _persistent);
            default:
                Debug.LogError($"[PickupableItem] Unknown item type: {_itemType}");
                return null;
        }
    }
    
    [Command(requiresAuthority = false)]
    private void CmdPickupItem(NetworkConnectionToClient sender = null)
    {
        if (_isPickedUp)
        {
            Debug.Log("[PickupableItem] Item already picked up");
            return;
        }
        
        _isPickedUp = true;

        OnPickedUpLocally();
        RpcOnPickedUp();
        
        NetworkServer.Destroy(gameObject);
    }
    
    [ClientRpc]
    private void RpcOnPickedUp()
    {
        OnPickedUpLocally();
    }
    
    private void OnPickedUpLocally()
    {
        InteractHintUI.Hide();
        
        if (interactZone != null)
        {
            Destroy(interactZone.gameObject);
        }
    }
}

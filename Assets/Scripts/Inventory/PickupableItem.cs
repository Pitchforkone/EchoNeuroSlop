using UnityEngine;
using Mirror;

/// <summary>
/// Компонент для предметов на сцене, которые можно подобрать голосовой командой "Take".
/// Использует VoiceActivateZoneMB для отображения подсказки и IVoiceWordListener для обработки команды.
/// При подборе предмет исчезает на всех клиентах и добавляется в инвентарь игрока.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PickupableItem : NetworkBehaviour, IVoiceWordListener
{
    [Header("Voice Zone")]
    [Tooltip("Ссылка на дочерний объект с VoiceActivateZoneMB")]
    public VoiceActivateZoneMB voiceZone;
    
    [Header("Item Settings")]
    [Tooltip("Тип предмета, который будет добавлен в инвентарь")]
    [SerializeField] private PickupItemType _itemType = PickupItemType.ExitKey;
    
    [Tooltip("Количество предметов при подборе")]
    [SerializeField] private int _count = 1;

    private readonly string _pickupKeyword = "Take";
    private VoiceRecognizer _currentRecognizer;
    
    [SyncVar]
    private bool _isPickedUp = false;
    
    public enum PickupItemType
    {
        ExitKey,
        Grenade
    }
    
    private void Start()
    {
        if (voiceZone == null)
        {
            Debug.LogError("[PickupableItem] VoiceActivateZoneMB not assigned!");
            return;
        }
        
        voiceZone.SetKeyword("Take");
        
        // Подписываемся на события зоны
        voiceZone.activate += OnPlayerEnterZone;
        voiceZone.deactivate += OnPlayerExitZone;
    }
    
    private void OnDestroy()
    {
        if (voiceZone != null)
        {
            voiceZone.activate -= OnPlayerEnterZone;
            voiceZone.deactivate -= OnPlayerExitZone;
        }
        
        // Отписываемся от VoiceRecognizer при уничтожении
        if (_currentRecognizer != null)
        {
            _currentRecognizer.RemoveListener(this);
            _currentRecognizer = null;
        }
    }
    
    private void OnPlayerEnterZone(VoiceRecognizer recognizer)
    {
        if (_isPickedUp) return;
        
        // Подписываемся только на локального игрока
        if (recognizer == VoiceRecognizer.LocalInstance)
        {
            _currentRecognizer = recognizer;
            recognizer.AddListener(this);
        }
    }
    
    private void OnPlayerExitZone(VoiceRecognizer recognizer)
    {
        // Отписываемся только от локального игрока
        if (recognizer == VoiceRecognizer.LocalInstance && _currentRecognizer == recognizer)
        {
            recognizer.RemoveListener(this);
            _currentRecognizer = null;
            Debug.Log($"[PickupableItem] Player exited pickup zone for {_itemType}");
            
            // Если предмет уже подобран, уничтожаем зону
            if (_isPickedUp && voiceZone != null)
            {
                Destroy(voiceZone.gameObject);
            }
        }
    }
    
    /// <summary>
    /// Вызывается при распознавании слова.
    /// </summary>
    public void OnWordRecognized(string word)
    {
        if (_isPickedUp) return;
        if (string.IsNullOrEmpty(word)) return;
        
        // Проверяем ключевое слово подбора
        if (string.Equals(word, _pickupKeyword, System.StringComparison.OrdinalIgnoreCase))
        {
            
            // Добавляем предмет в инвентарь локально
            AddItemToLocalInventory();
            
            // Отправляем команду на сервер для удаления объекта
            CmdPickupItem();
        }
    }
    
    private void AddItemToLocalInventory()
    {
        if (PlayerInventory.LocalInstance == null)
        {
            Debug.LogError("[PickupableItem] PlayerInventory.LocalInstance is null!");
            return;
        }
        
        // Создаём предмет в зависимости от типа
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
                return new ExitKeyItem(_count);
            case PickupItemType.Grenade:
                return new GrenadeItem(_count);
            default:
                Debug.LogError($"[PickupableItem] Unknown item type: {_itemType}");
                return null;
        }
    }
    
    /// <summary>
    /// Команда на сервер для подбора предмета.
    /// </summary>
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
        
        // Уничтожаем объект на сервере (автоматически синхронизируется со всеми клиентами)
        NetworkServer.Destroy(gameObject);
    }
    
    [ClientRpc]
    private void RpcOnPickedUp()
    {
        OnPickedUpLocally();
    }
    
    private void OnPickedUpLocally()
    {
        // Отписываем слушателя
        if (_currentRecognizer != null)
        {
            _currentRecognizer.RemoveListener(this);
            _currentRecognizer = null;
        }
        
        // Скрываем UI подсказку
        VoiceHintUI.Hide();
        
        // Уничтожаем зону активации
        if (voiceZone != null)
        {
            Destroy(voiceZone.gameObject);
        }
    }
}

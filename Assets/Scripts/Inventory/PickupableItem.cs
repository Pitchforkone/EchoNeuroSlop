using UnityEngine;
using Photon.Pun;

/// <summary>
/// Компонент для предметов на сцене, которые можно подбирать командой "Take".
/// При подборе предмет удаляется на всех клиентах и добавляется в инвентарь.
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class PickupableItem : MonoBehaviourPun, IVoiceWordListener
{
    [Header("Voice Zone")]
    public VoiceActivateZoneMB voiceZone;
    
    [Header("Item Settings")]
    [SerializeField] private PickupItemType _itemType = PickupItemType.ExitKey;
    [SerializeField] private int _count = 1;

    private readonly string _pickupKeyword = "Take";
    private VoiceRecognizer _currentRecognizer;
    
    private bool _isPickedUp = false;
    
    public enum PickupItemType
    {
        ExitKey,
        Grenade,
        NightVision
    }
    
    private void Start()
    {
        if (voiceZone == null)
        {
            Debug.LogError("[PickupableItem] VoiceActivateZoneMB not assigned!");
            return;
        }
        
        voiceZone.SetKeyword("Take");
        
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
        
        if (_currentRecognizer != null)
        {
            _currentRecognizer.RemoveListener(this);
            _currentRecognizer = null;
        }
    }
    
    private void OnPlayerEnterZone(VoiceRecognizer recognizer)
    {
        if (_isPickedUp) return;
        
        if (recognizer == VoiceRecognizer.LocalInstance)
        {
            _currentRecognizer = recognizer;
            recognizer.AddListener(this);
        }
    }
    
    private void OnPlayerExitZone(VoiceRecognizer recognizer)
    {
        if (recognizer == VoiceRecognizer.LocalInstance && _currentRecognizer == recognizer)
        {
            recognizer.RemoveListener(this);
            _currentRecognizer = null;
            Debug.Log($"[PickupableItem] Player exited pickup zone for {_itemType}");
            
            if (_isPickedUp && voiceZone != null)
            {
                Destroy(voiceZone.gameObject);
            }
        }
    }
    
    public void OnWordRecognized(string word)
    {
        if (_isPickedUp) return;
        if (string.IsNullOrEmpty(word)) return;
        
        if (string.Equals(word, _pickupKeyword, System.StringComparison.OrdinalIgnoreCase))
        {
            AddItemToLocalInventory();
            photonView.RPC(nameof(RpcOnPickedUp), RpcTarget.All);
        }
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
                return new ExitKeyItem(_count);
            case PickupItemType.Grenade:
                return new GrenadeItem(_count);
            case PickupItemType.NightVision:
                return new NightVisionItem(_count);
            default:
                Debug.LogError($"[PickupableItem] Unknown item type: {_itemType}");
                return null;
        }
    }
    
    [PunRPC]
    private void RpcOnPickedUp()
    {
        if (_isPickedUp) return;
        _isPickedUp = true;
        
        OnPickedUpLocally();
        
        // MasterClient destroys the networked object
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
    
    private void OnPickedUpLocally()
    {
        if (_currentRecognizer != null)
        {
            _currentRecognizer.RemoveListener(this);
            _currentRecognizer = null;
        }
        
        VoiceHintUI.Hide();
        
        if (voiceZone != null)
        {
            Destroy(voiceZone.gameObject);
        }
    }
}

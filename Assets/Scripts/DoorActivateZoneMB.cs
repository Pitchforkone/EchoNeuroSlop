using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

public class DoorActivateZoneMB : MonoBehaviourPun, IVoiceWordListener
{
    public VoiceActivateZoneMB activateZoneMB;
    private List<VoiceRecognizer> listPlayer = new();
    
    private bool isOpened = false;
    
    public string KeyWord = "Open";
    
    public void Start()
    {
        activateZoneMB.SetKeyword(KeyWord);
        activateZoneMB.activate += OnActivate;
        activateZoneMB.deactivate += OnDeactivate;
    }
    
    public void OnActivate(VoiceRecognizer voice)
    {
        if (isOpened) return;
        
        voice.AddListener(this);
        listPlayer.Add(voice);
    }
    
    public void OnDeactivate(VoiceRecognizer voice)
    {
        voice.RemoveListener(this);
        listPlayer.Remove(voice);
        
        if (isOpened)
        {
            Destroy(activateZoneMB.gameObject);
        }
    }

    public void OnWordRecognized(string word)
    {
        if (isOpened) return;
        
        // Special handling for "Key" keyword - requires key item in inventory
        if (string.Equals(KeyWord, "Key", System.StringComparison.OrdinalIgnoreCase) &&
            string.Equals(word, KeyWord, System.StringComparison.OrdinalIgnoreCase))
        {
            if (PlayerInventory.LocalInstance == null)
            {
                return;
            }
            
            var keyItem = PlayerInventory.LocalInstance.FindItemByKeyword("key");
            if (keyItem != null)
            {
                PlayerInventory.LocalInstance.UseItem(keyItem);
                photonView.RPC(nameof(RpcOpenDoor), RpcTarget.All);
            }
            return;
        }
        
        // Normal keyword handling
        if (string.Equals(word, KeyWord, System.StringComparison.OrdinalIgnoreCase))
        {
            photonView.RPC(nameof(RpcOpenDoor), RpcTarget.All);
        }
    }
    
    [PunRPC]
    private void RpcOpenDoor()
    {
        if (isOpened) return;
        isOpened = true;
        OpenDoorLocally();
    }
    
    private void OpenDoorLocally()
    {
        listPlayer.ForEach(voice => voice.RemoveListener(this));
        listPlayer.Clear();
        GetComponent<Animator>().SetBool("Open", true);
        
        VoiceHintUI.Hide();
        
        if (activateZoneMB != null)
        {
            Destroy(activateZoneMB.gameObject);
        }
    }
}

using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DoorActivateZoneMB : NetworkBehaviour, IVoiceWordListener
{
    public VoiceActivateZoneMB activateZoneMB;
    private List<VoiceRecognizer> listPlayer = new();
    
    [SyncVar]
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
        if (string.Equals(KeyWord, "Key", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(word, KeyWord, StringComparison.OrdinalIgnoreCase))
        {
            // Check if player has a key in inventory
            if (PlayerInventory.LocalInstance == null)
            {
                return;
            }
            
            var keyItem = PlayerInventory.LocalInstance.FindItemByKeyword("key");
            if (keyItem != null)
            {
                // Use the key (removes it from inventory if count becomes 0)
                PlayerInventory.LocalInstance.UseItem(keyItem);
                CmdOpenDoor();
            }
            return;
        }
        
        // Normal keyword handling (no inventory check required)
        if (string.Equals(word, KeyWord, StringComparison.OrdinalIgnoreCase))
        {
            CmdOpenDoor();
        }
    }
    
    [Command(requiresAuthority = false)]
    private void CmdOpenDoor()
    {
        if (isOpened) return;
        
        isOpened = true;
        RpcOpenDoor();
    }
    
    [ClientRpc]
    private void RpcOpenDoor()
    {
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

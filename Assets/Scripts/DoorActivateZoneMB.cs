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

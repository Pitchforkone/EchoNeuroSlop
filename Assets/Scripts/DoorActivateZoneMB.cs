using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class DoorActivateZoneMB : NetworkBehaviour
{
    public InteractZone interactZone;
    
    [SyncVar]
    private bool isOpened = false;
    
    public string HintText = "Open";
    
    public void Start()
    {
        interactZone.SetHintText(HintText);
        interactZone.interact += OnInteract;
    }
    
    private void OnDestroy()
    {
        if (interactZone != null)
        {
            interactZone.interact -= OnInteract;
        }
    }

    private void OnInteract()
    {
        if (isOpened) return;
        CmdOpenDoor();
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
        GetComponent<Animator>().SetBool("Open", true);
        
        InteractHintUI.Hide();
        
        if (interactZone != null)
        {
            Destroy(interactZone.gameObject);
        }
    }
}

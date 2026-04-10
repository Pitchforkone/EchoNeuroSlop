using System.Collections.Generic;
using Mirror;
using UnityEngine;

public enum DoorOpenCondition
{
    None,
    RequiresExitKey,
    RequiresRoomKey
}

public enum KeyColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Orange,
    Purple
}

public class DoorActivateZoneMB : NetworkBehaviour
{
    public InteractZone interactZone;

    [SyncVar]
    private bool isOpened = false;

    public string HintText = "Open";

    public DoorOpenCondition openCondition = DoorOpenCondition.None;

    [Tooltip("Цвет ключа, необходимого для открытия двери (только для RequiresRoomKey)")]
    public KeyColor requiredKeyColor = KeyColor.Red;

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

        if (!CheckOpenCondition()) return;

        CmdOpenDoor();
    }

    private bool CheckOpenCondition()
    {
        switch (openCondition)
        {
            case DoorOpenCondition.RequiresExitKey:
                var inventory = PlayerInventory.LocalInstance;
                if (inventory == null) return false;

                for (int i = 0; i < PlayerInventory.SlotCount; i++)
                {
                    if (inventory.GetSlot(i) is ExitKeyItem)
                        return true;
                }

                Debug.Log("[DoorActivateZoneMB] Requires Exit Key to open");
                return false;

            case DoorOpenCondition.RequiresRoomKey:
                var inv = PlayerInventory.LocalInstance;
                if (inv == null) return false;

                for (int i = 0; i < PlayerInventory.SlotCount; i++)
                {
                    if (inv.GetSlot(i) is RoomKeyItem roomKey && roomKey.KeyColor == requiredKeyColor)
                        return true;
                }

                Debug.Log($"[DoorActivateZoneMB] Requires {requiredKeyColor} Key to open");
                return false;

            case DoorOpenCondition.None:
            default:
                return true;
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
        GetComponent<Animator>().SetBool("Open", true);
        
        InteractHintUI.Hide();
        
        if (interactZone != null)
        {
            Destroy(interactZone.gameObject);
        }
    }
}

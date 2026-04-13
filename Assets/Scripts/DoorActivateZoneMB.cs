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
        interactZone.SetDynamicHintProvider(GetDynamicHintText);
        interactZone.interact += OnInteract;
    }

    private void OnDestroy()
    {
        if (interactZone != null)
        {
            interactZone.interact -= OnInteract;
        }
    }

    private string GetDynamicHintText()
    {
        switch (openCondition)
        {
            case DoorOpenCondition.RequiresExitKey:
                if (HasExitKey())
                    return "Press E to open";
                return "Requires an Exit Key to open";

            case DoorOpenCondition.RequiresRoomKey:
                if (HasRoomKey(requiredKeyColor))
                    return "Press E to open";
                return $"Requires a {requiredKeyColor} Key to open";

            case DoorOpenCondition.None:
            default:
                return HintText;
        }
    }

    private bool HasExitKey()
    {
        var inventory = PlayerInventory.LocalInstance;
        if (inventory == null) return false;

        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            if (inventory.GetSlot(i) is ExitKeyItem)
                return true;
        }
        return false;
    }

    private bool HasRoomKey(KeyColor color)
    {
        var inventory = PlayerInventory.LocalInstance;
        if (inventory == null) return false;

        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            if (inventory.GetSlot(i) is RoomKeyItem roomKey && roomKey.KeyColor == color)
                return true;
        }
        return false;
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
                if (!HasExitKey())
                {
                    Debug.Log("[DoorActivateZoneMB] Requires Exit Key to open");
                    return false;
                }
                return true;

            case DoorOpenCondition.RequiresRoomKey:
                if (!HasRoomKey(requiredKeyColor))
                {
                    Debug.Log($"[DoorActivateZoneMB] Requires {requiredKeyColor} Key to open");
                    return false;
                }
                return true;

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

using Mirror;
using UnityEngine;

/// <summary>
/// Зона активации вентиляции: для открытия требуется отвёртка (ScrewdriverItem).
/// При открытии к решёткам выходов применяется сила выброса.
/// </summary>
public class VentActivateZoneMB : NetworkBehaviour
{

    [Header("Exit Points")]
    [Tooltip("Первый выход вентиляции (решётка)")]
    public GameObject exitPoint1;
    [Tooltip("Зона взаимодействия первого выхода")]
    public InteractZone exitZone1;

    [Tooltip("Второй выход вентиляции (решётка)")]
    public GameObject exitPoint2;
    [Tooltip("Зона взаимодействия второго выхода")]
    public InteractZone exitZone2;

    [Header("Physics Settings")]
    [Tooltip("Сила выброса решёток при открытии")]
    public float ejectForce = 5f;

    [SyncVar]
    private bool isOpened = false;

    [Tooltip("Должна ли отвёртка расходоваться при открытии")]
    public bool consumeScrewdriver = false;

    public void Start()
    {
        exitZone1.SetDynamicHintProvider(GetDynamicHintText);
        exitZone1.interact += OnInteract;
        exitZone2.SetDynamicHintProvider(GetDynamicHintText);
        exitZone2.interact += OnInteract;
    }

    private void OnDestroy()
    {
        if (exitZone2 != null)
        {
            exitZone2.interact -= OnInteract;
        }
        if (exitZone1 != null)
        {
            exitZone1.interact -= OnInteract;
        }
    }

    private string GetDynamicHintText()
    {
        if (HasScrewdriver())
            return "Press E to open vent";
        return "Requires a Screwdriver to open";
    }

    private bool HasScrewdriver()
    {
        var inventory = PlayerInventory.LocalInstance;
        if (inventory == null) return false;

        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            if (inventory.GetSlot(i) is ScrewdriverItem)
                return true;
        }
        return false;
    }

    private void OnInteract()
    {
        if (isOpened) return;

        if (!HasScrewdriver())
        {
            Debug.Log("[VentActivateZoneMB] Requires Screwdriver to open");
            return;
        }

        if (consumeScrewdriver)
        {
            ConsumeScrewdriver();
        }

        CmdOpenVent();
    }

    private void ConsumeScrewdriver()
    {
        var inventory = PlayerInventory.LocalInstance;
        if (inventory == null) return;

        for (int i = 0; i < PlayerInventory.SlotCount; i++)
        {
            var item = inventory.GetSlot(i);
            if (item is ScrewdriverItem)
            {
                inventory.RemoveItem(item);
                break;
            }
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdOpenVent()
    {
        if (isOpened) return;

        isOpened = true;
        RpcOpenVent();
    }

    [ClientRpc]
    private void RpcOpenVent()
    {
        OpenVentLocally();
    }

    private void OpenVentLocally()
    {
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("Open", true);
        }

        EjectExitPoint(exitPoint1, exitZone1);
        EjectExitPoint(exitPoint2, exitZone2);

        InteractHintUI.Hide();

        if (exitPoint1 != null)
        {
            Destroy(exitPoint1.gameObject);
        }
        if (exitPoint2 != null)
        {
            Destroy(exitPoint2.gameObject);
        }
    }

    private void EjectExitPoint(GameObject exitPoint, InteractZone exitZone)
    {
        if (exitPoint == null) return;

        var rb = exitPoint.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = exitPoint.AddComponent<Rigidbody>();
        }

        rb.isKinematic = false;
        rb.AddForce(exitPoint.transform.forward * ejectForce, ForceMode.Impulse);

        if (exitZone != null)
        {
            Destroy(exitZone.gameObject);
        }
    }
}

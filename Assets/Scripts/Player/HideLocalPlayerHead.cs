using UnityEngine;
using Mirror;

/// <summary>
/// Place this script on the head object.
/// Disables the head GameObject for the local player.
/// </summary>
public class HideLocalPlayerHead : MonoBehaviour
{
    private void Start()
    {
        var networkIdentity = GetComponentInParent<NetworkIdentity>();

        // Multiplayer: hide only for local player
        if (networkIdentity != null && networkIdentity.isLocalPlayer)
        {
            gameObject.SetActive(false);
        }
        // Single player: always hide
        else if (!NetworkClient.active)
        {
            gameObject.SetActive(false);
        }
    }
}

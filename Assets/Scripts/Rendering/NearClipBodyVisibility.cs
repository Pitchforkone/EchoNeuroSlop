using UnityEngine;
using Mirror;

/// <summary>
/// Uses camera near clip plane to hide close objects (head) while showing distant parts (body, legs).
/// Works with single mesh objects.
/// </summary>
public class NearClipBodyVisibility : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Main camera (will be found automatically if not set)")]
    [SerializeField] private Camera mainCamera;

    [Header("Clipping Distance")]
    [Tooltip("Distance from camera where mesh starts being visible")]
    [SerializeField] private float nearClipDistance = 0.3f;

    [Tooltip("Default near clip for when not local player")]
    [SerializeField] private float defaultNearClip = 0.01f;

    private float originalNearClip;
    private NetworkIdentity networkIdentity;

    private void Awake()
    {
        networkIdentity = GetComponent<NetworkIdentity>();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            originalNearClip = mainCamera.nearClipPlane;
        }
    }

    private void Start()
    {
        // Only for local player or single player
        bool isLocalPlayer = false;

        if (!NetworkClient.active)
        {
            isLocalPlayer = true; // Single player mode
        }
        else if (networkIdentity != null && networkIdentity.isLocalPlayer)
        {
            isLocalPlayer = true; // Multiplayer local player
        }

        if (isLocalPlayer && mainCamera != null)
        {
            mainCamera.nearClipPlane = nearClipDistance;
        }
        else
        {
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (mainCamera != null)
        {
            mainCamera.nearClipPlane = defaultNearClip;
        }
    }

    private void OnDisable()
    {
        if (mainCamera != null)
        {
            mainCamera.nearClipPlane = defaultNearClip;
        }
    }
}

using UnityEngine;

/// <summary>
/// Component that adds a small static spherical trigger collider to an echo object.
/// Contains the EchoPreset that spawned this echo.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class EchoCollider : MonoBehaviour
{
    private const float ColliderRadius = 0.5f;

    private SphereCollider _sphereCollider;

    /// <summary>
    /// The preset that was used to create this echo.
    /// </summary>
    public EchoPreset Preset { get; private set; }

    /// <summary>
    /// Initializes the echo collider with the specified preset.
    /// </summary>
    /// <param name="preset">The echo preset</param>
    public void Initialize(EchoPreset preset)
    {
        Preset = preset;

        _sphereCollider = GetComponent<SphereCollider>();
        _sphereCollider.isTrigger = true;
        _sphereCollider.radius = ColliderRadius;
    }

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        if (_sphereCollider == null)
        {
            _sphereCollider = gameObject.AddComponent<SphereCollider>();
        }
        _sphereCollider.isTrigger = true;
        _sphereCollider.radius = ColliderRadius;
    }

    private void OnTriggerEnter(Collider other)
    {
    }

    private void OnDrawGizmosSelected()
    {
        if (_sphereCollider == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _sphereCollider.radius);
    }
}

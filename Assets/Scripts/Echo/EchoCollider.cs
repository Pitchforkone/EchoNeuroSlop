using UnityEngine;

/// <summary>
/// Component that adds a small static spherical trigger collider to an echo object.
/// Contains the EchoType that defines this echo.
/// Includes a kinematic Rigidbody to enable trigger-trigger collision detection.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class EchoCollider : MonoBehaviour
{
    private const float ColliderRadius = 0.5f;

    private SphereCollider _sphereCollider;
    private Rigidbody _rigidbody;

    /// <summary>
    /// The type of echo.
    /// </summary>
    public EchoType EchoType;

    /// <summary>
    /// Initializes the echo collider with the specified echo type directly.
    /// </summary>
    /// <param name="echoType">The echo type</param>
    public void Initialize(EchoType echoType)
    {
        EchoType = echoType;
    }

    private void Awake()
    {
        // Setup SphereCollider
        _sphereCollider = GetComponent<SphereCollider>();
        if (_sphereCollider == null)
        {
            _sphereCollider = gameObject.AddComponent<SphereCollider>();
        }
        _sphereCollider.isTrigger = true;
        _sphereCollider.radius = ColliderRadius;

        // Setup Rigidbody as kinematic (required for trigger-trigger collision detection)
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (_sphereCollider == null) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, _sphereCollider.radius);
    }
}

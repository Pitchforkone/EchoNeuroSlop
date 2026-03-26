using UnityEngine;

/// <summary>
/// Component that receives echo collisions and distance information.
/// Attach to any object that should react to echo pulses.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EchoReceiver : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Layer mask for filtering which echoes affect this receiver")]
    [SerializeField] private LayerMask _echoLayerMask = ~0;
    public float AgroSum;
    public float Current;

    private void Awake()
    {
        // Ensure our collider is set as trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[EchoReceiver] Collider on {gameObject.name} should be set as Trigger for echo detection.", this);
        }
    }

    /// <summary>
    /// Called when an echo pulse reaches this object.
    /// </summary>
    /// <param name="distance">Distance from the echo origin to this object</param>
    public virtual void OnEchoReached(float distance)
    {
        Current = distance;
        AgroSum += distance;
    }

    /// <summary>
    /// Called when an echo pulse reaches this object with full echo information.
    /// </summary>
    /// <param name="distance">Distance from the echo origin to this object</param>
    /// <param name="echoCollider">The EchoCollider that triggered this</param>
    public virtual void OnEchoReached(float distance, EchoCollider echoCollider)
    {
        OnEchoReached(distance);
    }

    private void OnTriggerEnter(Collider other)
    { 

        var echoCollider = other.GetComponent<EchoCollider>();
        if (echoCollider == null) return;

        // Distance from echo origin to this receiver
        float distance = Vector3.Distance(echoCollider.transform.position, transform.position);
        OnEchoReached(distance, echoCollider);
    }
}

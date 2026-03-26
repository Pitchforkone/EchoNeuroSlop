using UnityEngine;

/// <summary>
/// Component that adds an expanding spherical collider to an echo object.
/// The collider expands over time based on echo parameters.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class EchoCollider : MonoBehaviour
{
    private SphereCollider _sphereCollider;
    private float _startTime;
    private float _speed;
    private float _maxRadius;
    private float _lifetime;
    private bool _isInitialized;

    /// <summary>
    /// Initializes the echo collider with the specified parameters.
    /// </summary>
    /// <param name="speed">Expansion speed in m/s</param>
    /// <param name="maxRadius">Maximum radius the collider can reach</param>
    /// <param name="lifetime">Total lifetime in seconds</param>
    public void Initialize(float speed, float maxRadius, float lifetime)
    {
        _speed = speed;
        _maxRadius = maxRadius;
        _lifetime = lifetime;
        _startTime = Time.time;
        _isInitialized = true;

        _sphereCollider = GetComponent<SphereCollider>();
        _sphereCollider.isTrigger = true;
        _sphereCollider.radius = 0.1f;
    }

    /// <summary>
    /// Initializes the echo collider using an EchoPreset.
    /// </summary>
    /// <param name="preset">The echo preset containing parameters</param>
    public void Initialize(EchoPreset preset)
    {
        if (preset == null) return;
        Initialize(preset.Speed, preset.MaxRadius, preset.Lifetime);
    }

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        if (_sphereCollider == null)
        {
            _sphereCollider = gameObject.AddComponent<SphereCollider>();
        }
        _sphereCollider.isTrigger = true;
    }

    private void Update()
    {
        if (!_isInitialized) return;

        float elapsed = Time.time - _startTime;

        // Check if lifetime exceeded
        if (elapsed >= _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Expand the collider radius
        float currentRadius = Mathf.Min(_speed * elapsed, _maxRadius);
        _sphereCollider.radius = currentRadius;
    }

    /// <summary>
    /// Gets the current radius of the echo collider.
    /// </summary>
    public float CurrentRadius => _sphereCollider != null ? _sphereCollider.radius : 0f;

    /// <summary>
    /// Gets the normalized progress of the echo (0 to 1).
    /// </summary>
    public float NormalizedProgress
    {
        get
        {
            if (!_isInitialized || _lifetime <= 0f) return 0f;
            return Mathf.Clamp01((Time.time - _startTime) / _lifetime);
        }
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

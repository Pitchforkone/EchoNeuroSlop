using UnityEngine;

/// <summary>
/// Configuration preset for an echo pulse (speed, radius, color, intensity, lifetime).
/// Create via Assets → Create → Echo/EchoPreset.
/// </summary>
[CreateAssetMenu(fileName = "NewEchoPreset", menuName = "Echo/EchoPreset")]
public class EchoPreset : ScriptableObject
{
    [Header("Wave")]
    [Tooltip("Propagation speed in m/s")]
    [SerializeField] private float _speed = 15f;

    [Tooltip("Maximum radius the wave can reach")]
    [SerializeField] private float _maxRadius = 20f;

    [Header("Visuals")]
    [SerializeField] private Color _color = Color.white;

    [Tooltip("Starting light intensity")]
    [SerializeField] private float _intensity = 3f;

    [Header("Timing")]
    [Tooltip("Total lifetime in seconds (auto-calculated from speed & radius if 0)")]
    [SerializeField] private float _lifetimeOverride;

    public float Speed => _speed;
    public float MaxRadius => _maxRadius;
    public Color Color => _color;
    public float Intensity => _intensity;

    /// <summary>
    /// Lifetime in seconds. If override is 0, calculated as MaxRadius / Speed.
    /// </summary>
    public float Lifetime => _lifetimeOverride > 0f ? _lifetimeOverride : _maxRadius / _speed;
}

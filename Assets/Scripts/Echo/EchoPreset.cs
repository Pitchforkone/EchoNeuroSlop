using UnityEngine;

/// <summary>
/// Configuration preset for an echo pulse (speed, radius, color, intensity, lifetime).
/// Create via Assets → Create → Echo/EchoPreset.
/// </summary>
[CreateAssetMenu(fileName = "NewEchoPreset", menuName = "Echo/EchoPreset")]
public class EchoPreset : ScriptableObject
{
    [Header("Type")]
    [Tooltip("The type of this echo")]
    [SerializeField] private EchoType _echoType = EchoType.NonTrigger;

    [Header("Wave")]
    [Tooltip("Propagation speed in m/s")]
    [SerializeField] private float _speed = 15f;

    [Tooltip("Maximum radius the wave can reach")]
    [SerializeField] private float _maxRadius = 20f;

    [Header("Visuals")]
    [SerializeField] private Color _color = Color.white;

    [Header("Intensity over Time")]
    [Tooltip("X = time (seconds), Y = light intensity. Echo dies when the curve ends.")]
    [SerializeField] private AnimationCurve _intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.1f, 3f),
        new Keyframe(0.4f, 3f),
        new Keyframe(1.33f, 0f)
    );

    public EchoType EchoType => _echoType;
    public float Speed => _speed;
    public float MaxRadius => _maxRadius;
    public Color Color => _color;
    public AnimationCurve IntensityCurve => _intensityCurve;

    /// <summary>
    /// Total lifetime in seconds (time of the last keyframe).
    /// </summary>
    public float Lifetime
    {
        get
        {
            if (_intensityCurve == null || _intensityCurve.length == 0)
                return _maxRadius / _speed;
            return _intensityCurve.keys[_intensityCurve.length - 1].time;
        }
    }

    /// <summary>
    /// Peak intensity value across all keyframes.
    /// </summary>
    public float PeakIntensity
    {
        get
        {
            if (_intensityCurve == null || _intensityCurve.length == 0) return 0f;
            float max = 0f;
            foreach (var key in _intensityCurve.keys)
                if (key.value > max) max = key.value;
            return max;
        }
    }
}

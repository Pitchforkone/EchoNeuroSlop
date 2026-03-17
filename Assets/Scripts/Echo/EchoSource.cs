using UnityEngine;

/// <summary>
/// Generic echo source component. Can fire a single pulse or repeat on an interval.
/// Uses EchoPreset for configuration. Registers with EchoManager.
/// </summary>
public class EchoSource : MonoBehaviour
{
    [SerializeField] private EchoPreset _preset;
    [SerializeField] private bool _fireOnStart;
    [SerializeField] private bool _repeating;
    [SerializeField] private float _repeatInterval = 3f;

    private float _nextFireTime;

    private void Start()
    {
        if (_fireOnStart)
            Fire();

        if (_repeating)
            _nextFireTime = Time.time + _repeatInterval;
    }

    private void Update()
    {
        if (!_repeating) return;

        if (Time.time >= _nextFireTime)
        {
            Fire();
            _nextFireTime = Time.time + _repeatInterval;
        }
    }

    /// <summary>
    /// Trigger an echo pulse at this object's position using the assigned preset.
    /// </summary>
    public void Fire()
    {
        if (_preset == null || EchoManager.Instance == null) return;
        EchoManager.Instance.SpawnEcho(transform.position, _preset);
    }

    /// <summary>
    /// Trigger an echo pulse with an explicit preset (overrides the serialized one).
    /// </summary>
    public void Fire(EchoPreset preset)
    {
        if (preset == null || EchoManager.Instance == null) return;
        EchoManager.Instance.SpawnEcho(transform.position, preset);
    }
}

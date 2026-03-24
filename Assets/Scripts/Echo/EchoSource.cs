using UnityEngine;

/// <summary>
/// Ambient echo source — fires local-only pulses (no network traffic).
/// Uses a separate pool in EchoManager that doesn't compete with player echo slots.
/// Place on scene objects: dripping water, vents, electrical hum, etc.
/// </summary>
public class EchoSource : MonoBehaviour
{
    [SerializeField] private EchoPreset _preset;
    [SerializeField] private bool _fireOnStart;
    [SerializeField] private bool _repeating;
    [SerializeField] private float _repeatInterval = 3f;

    [Tooltip("Random ± offset added to each repeat interval to avoid sync between sources")]
    [SerializeField] private float _intervalJitter = 0.5f;

    private float _nextFireTime;

    private void Start()
    {
        if (_fireOnStart)
            Fire();

        if (_repeating)
            _nextFireTime = Time.time + _repeatInterval + Random.Range(-_intervalJitter, _intervalJitter);
    }

    private void Update()
    {
        if (!_repeating) return;

        if (Time.time >= _nextFireTime)
        {
            Fire();
            _nextFireTime = Time.time + _repeatInterval + Random.Range(-_intervalJitter, _intervalJitter);
        }
    }

    /// <summary>
    /// Trigger an ambient echo pulse at this object's position using the assigned preset.
    /// Local-only — does not go through network.
    /// </summary>
    public void Fire()
    {
        if (_preset == null || EchoManager.Instance == null) return;
        EchoManager.Instance.SpawnAmbientEcho(transform.position, _preset);
    }

    /// <summary>
    /// Trigger an ambient echo pulse with an explicit preset.
    /// </summary>
    public void Fire(EchoPreset preset)
    {
        if (preset == null || EchoManager.Instance == null) return;
        EchoManager.Instance.SpawnAmbientEcho(transform.position, preset);
    }
}

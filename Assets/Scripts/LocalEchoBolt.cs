using UnityEngine;

/// <summary>
/// Локальная версия болта для синглплеера.
/// </summary>
public class LocalEchoBolt : MonoBehaviour
{
    private EchoPreset _echoPreset;
    private int _maxCollisions;
    private float _maxLifetime;
    private int _currentCollisions;
    private float _spawnTime;
    private bool _initialized;

    public void Initialize(EchoPreset echoPreset, int maxCollisions, float maxLifetime)
    {
        _echoPreset = echoPreset;
        _maxCollisions = maxCollisions;
        _maxLifetime = maxLifetime;
        _currentCollisions = 0;
        _spawnTime = Time.time;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        if (Time.time - _spawnTime >= _maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("FFFFFFFFF0");
        if (!_initialized) return;
        Debug.Log("FFFFFFFFF");
        Vector3 contactPoint = collision.contacts[0].point;

        // Создаём эхо
        if (_echoPreset != null && EchoManager.Instance != null)
        {
            EchoManager.Instance.SpawnEcho(contactPoint, _echoPreset);
        }

        _currentCollisions++;

        if (_currentCollisions >= _maxCollisions)
        {
            // Финальное усиленное эхо
            if (_echoPreset != null && EchoManager.Instance != null)
            {
                EchoManager.Instance.SpawnEcho(
                    contactPoint,
                    _echoPreset.Speed,
                    _echoPreset.MaxRadius * 1.5f,
                    _echoPreset.Intensity * 2f,
                    _echoPreset.Color,
                    _echoPreset.Lifetime * 1.5f
                );
            }
            Destroy(gameObject);
        }
    }
}

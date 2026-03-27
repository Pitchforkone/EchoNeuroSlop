using UnityEngine;

/// <summary>
/// Локальная версия болта для синглплеера.
/// </summary>
public class LocalEchoBolt : MonoBehaviour
{
    [Header("Настройки позиции эхо")]
    [Tooltip("Минимальная высота над точкой столкновения для спавна эхо")]
    [SerializeField] private float _minEchoHeight = 0.3f;

    private EchoPreset _echoPreset;
    private int _maxCollisions;
    private float _maxLifetime;
    private int _currentCollisions;
    private float _spawnTime;
    private bool _initialized;
    
    // Позиция в предыдущем кадре
    private Vector3 _previousPosition;
    private bool _hasPreviousPosition;

    public void Initialize(EchoPreset echoPreset, int maxCollisions, float maxLifetime)
    {
        _echoPreset = echoPreset;
        _maxCollisions = maxCollisions;
        _maxLifetime = maxLifetime;
        _currentCollisions = 0;
        _spawnTime = Time.time;
        _initialized = true;
        
        // Инициализируем предыдущую позицию
        _previousPosition = transform.position;
        _hasPreviousPosition = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        if (Time.time - _spawnTime >= _maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        // Сохраняем позицию для следующего кадра
        if (_initialized)
        {
            _previousPosition = transform.position;
            _hasPreviousPosition = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_initialized) return;

        // Определяем позицию для спавна эхо
        Vector3 echoPosition = GetEchoSpawnPosition(collision);

        // Создаём эхо
        if (_echoPreset != null && EchoManager.Instance != null)
        {
            EchoManager.Instance.SpawnEcho(echoPosition, _echoPreset);
        }

        _currentCollisions++;

        if (_currentCollisions >= _maxCollisions)
        {
            // Финальное усиленное эхо
            if (_echoPreset != null && EchoManager.Instance != null)
            {
                EchoManager.Instance.SpawnEcho(
                    echoPosition,
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

    /// <summary>
    /// Определяет позицию для спавна эхо.
    /// Использует предыдущую позицию если коллизия с полом.
    /// </summary>
    private Vector3 GetEchoSpawnPosition(Collision collision)
    {
        Vector3 contactPoint = collision.contacts[0].point;
        Vector3 contactNormal = collision.contacts[0].normal;
        
        // Проверяем, это коллизия с полом (нормаль направлена вверх)?
        bool isFloorCollision = Vector3.Dot(contactNormal, Vector3.up) > 0.7f;
        
        if (isFloorCollision && _hasPreviousPosition)
        {
            // Используем предыдущую позицию
            Vector3 echoPos = _previousPosition;
            
            // Убеждаемся что эхо не ниже минимальной высоты
            if (echoPos.y < contactPoint.y + _minEchoHeight)
            {
                echoPos.y = contactPoint.y + _minEchoHeight;
            }
            
            return echoPos;
        }
        
        // Для стен и потолков используем точку контакта со смещением по нормали
        return contactPoint + contactNormal * 0.1f;
    }
}

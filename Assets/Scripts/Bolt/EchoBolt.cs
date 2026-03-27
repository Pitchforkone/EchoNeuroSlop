using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// Компонент болта, который создаёт эхо при столкновении с объектами.
/// Уничтожается после определённого количества столкновений.
/// Синхронизируется по сети для всех игроков.
/// </summary>
public class EchoBolt : NetworkBehaviour
{
    [Header("Настройки эхо")]
    [Tooltip("Пресет эхо для болта")]
    [SerializeField] private EchoPreset _echoPresetLocal;

    [Header("Настройки коллизий")]
    [Tooltip("Максимальное количество столкновений до уничтожения")]
    [SyncVar]
    private int _maxCollisions = 3;

    [SyncVar]
    private int _currentCollisions;

    [Header("Время жизни")]
    [Tooltip("Максимальное время жизни болта (секунды)")]
    [SyncVar]
    private float _maxLifetime = 10f;

    [SyncVar]
    private float _spawnTime;

    [Header("Настройки позиции эхо")]
    [Tooltip("Минимальная высота над точкой столкновения для спавна эхо")]
    [SerializeField] private float _minEchoHeight = 0.3f;

    private EchoPreset _echoPreset;
    private Rigidbody _rigidbody;
    private bool _isReady;
    private bool _isServerInstance;
    
    // Позиция в предыдущем кадре
    private Vector3 _previousPosition;
    private bool _hasPreviousPosition;
    
    // Сохранённые параметры броска для отложенного применения
    private Vector3 _pendingVelocity;
    private Vector3 _pendingAngularVelocity;
    private bool _hasPendingForce;

    /// <summary>
    /// Инициализирует болт с заданными параметрами.
    /// Вызывается на сервере после спавна.
    /// </summary>
    [Server]
    public void Initialize(EchoPreset echoPreset, int maxCollisions, float maxLifetime, Vector3 velocity, Vector3 angularVelocity)
    {
        _echoPreset = echoPreset;
        _echoPresetLocal = echoPreset;
        _maxCollisions = maxCollisions;
        _maxLifetime = maxLifetime;
        _currentCollisions = 0;
        _spawnTime = Time.time;
        _isServerInstance = true;
        
        // Сохраняем параметры броска
        _pendingVelocity = velocity;
        _pendingAngularVelocity = angularVelocity;
        _hasPendingForce = true;
        
        // Инициализируем предыдущую позицию
        _previousPosition = transform.position;
        _hasPreviousPosition = true;
        
        // Запускаем отложенную активацию
        StartCoroutine(DelayedActivation());
    }

    private IEnumerator DelayedActivation()
    {
        // Ждём конец кадра чтобы Spawn полностью завершился
        yield return new WaitForEndOfFrame();
        
        // Теперь применяем физику
        if (_rigidbody != null && _hasPendingForce)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = _pendingVelocity;
            _rigidbody.angularVelocity = _pendingAngularVelocity;
            _hasPendingForce = false;
        }
        
        _isReady = true;
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        
        // Сразу делаем кинематическим
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _isServerInstance = true;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Инициализируем пресет на клиенте из сериализованного поля
        if (_echoPresetLocal != null)
        {
            _echoPreset = _echoPresetLocal;
        }
    }

    private void Update()
    {
        // Проверяем готовность перед любыми действиями
        if (!_isReady || !_isServerInstance) return;

        // Проверка времени жизни (только на сервере)
        if (Time.time - _spawnTime >= _maxLifetime)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        // Сохраняем позицию для следующего кадра (только на сервере)
        if (_isReady && _isServerInstance)
        {
            _previousPosition = transform.position;
            _hasPreviousPosition = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Проверяем готовность
        if (!_isReady || !_isServerInstance) return;

        // Определяем позицию для спавна эхо
        Vector3 echoPosition = GetEchoSpawnPosition(collision);
        
        // Создаём эхо
        SpawnEchoAtCollision(echoPosition);

        _currentCollisions++;

        if (_currentCollisions >= _maxCollisions)
        {
            // Финальное эхо перед уничтожением
            SpawnFinalEcho(echoPosition);
            NetworkServer.Destroy(gameObject);
        }
    }

    /// <summary>
    /// Определяет позицию для спавна эхо.
    /// Использует предыдущую позицию если коллизия с полом (нормаль направлена вверх).
    /// </summary>
    private Vector3 GetEchoSpawnPosition(Collision collision)
    {
        Vector3 contactPoint = collision.contacts[0].point;
        Vector3 contactNormal = collision.contacts[0].normal;
        
        // Проверяем, это коллизия с полом (нормаль направлена вверх)?
        bool isFloorCollision = Vector3.Dot(contactNormal, Vector3.up) > 0.7f;
        
        if (isFloorCollision && _hasPreviousPosition)
        {
            // Используем предыдущую позицию, но не ниже минимальной высоты над точкой контакта
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

    /// <summary>
    /// Создаёт эхо в точке столкновения.
    /// </summary>
    private void SpawnEchoAtCollision(Vector3 position)
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(position, _echoPreset);
    }

    /// <summary>
    /// Создаёт финальное усиленное эхо перед уничтожением.
    /// </summary>
    private void SpawnFinalEcho(Vector3 position)
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(
            position,
            _echoPreset.Speed,
            _echoPreset.MaxRadius * 1.5f,
            _echoPreset.Intensity * 2f,
            _echoPreset.Color,
            _echoPreset.Lifetime * 1.5f
        );
    }
}

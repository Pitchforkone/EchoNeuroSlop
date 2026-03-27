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

    private EchoPreset _echoPreset;
    private Rigidbody _rigidbody;
    private bool _isReady;
    private bool _isServerInstance; // Локальный флаг сервера
    
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
        _isServerInstance = true; // Мы на сервере
        
        // Сохраняем параметры броска
        _pendingVelocity = velocity;
        _pendingAngularVelocity = angularVelocity;
        _hasPendingForce = true;
        
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

    private void OnCollisionEnter(Collision collision)
    {
        // Проверяем готовность
        if (!_isReady || !_isServerInstance) return;

        // Создаём эхо на месте столкновения
        SpawnEchoAtCollision(collision.contacts[0].point);

        _currentCollisions++;

        if (_currentCollisions >= _maxCollisions)
        {
            // Финальное эхо перед уничтожением
            SpawnFinalEcho(collision.contacts[0].point);
            NetworkServer.Destroy(gameObject);
        }
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

using UnityEngine;
using Photon.Pun;
using System.Collections;

/// <summary>
/// Сетевой болт, который создаёт эхо при столкновении с поверхностью.
/// Уничтожается после определённого количества столкновений.
/// Синхронизируется по сети для всех клиентов.
/// </summary>
public class EchoBolt : MonoBehaviourPun
{
    [Header("Настройки эхо")]
    [SerializeField] private EchoPreset _echoPresetLocal;

    [Header("Настройки коллизий")]
    private int _maxCollisions = 3;
    private int _currentCollisions;

    [Header("Время жизни")]
    private float _maxLifetime = 10f;
    private float _spawnTime;

    [Header("Настройки позиции эхо")]
    [SerializeField] private float _minEchoHeight = 0.3f;

    private EchoPreset _echoPreset;
    private Rigidbody _rigidbody;
    private bool _isReady;
    private bool _isMasterInstance;
    
    private Vector3 _previousPosition;
    private bool _hasPreviousPosition;
    
    private Vector3 _pendingVelocity;
    private Vector3 _pendingAngularVelocity;
    private bool _hasPendingForce;

    /// <summary>
    /// Инициализирует болт с заданными параметрами.
    /// Вызывается на MasterClient после спавна.
    /// </summary>
    public void Initialize(EchoPreset echoPreset, int maxCollisions, float maxLifetime, Vector3 velocity, Vector3 angularVelocity)
    {
        _echoPreset = echoPreset;
        _echoPresetLocal = echoPreset;
        _maxCollisions = maxCollisions;
        _maxLifetime = maxLifetime;
        _currentCollisions = 0;
        _spawnTime = Time.time;
        _isMasterInstance = true;
        
        _pendingVelocity = velocity;
        _pendingAngularVelocity = angularVelocity;
        _hasPendingForce = true;
        
        _previousPosition = transform.position;
        _hasPreviousPosition = true;
        
        StartCoroutine(DelayedActivation());
    }

    private IEnumerator DelayedActivation()
    {
        yield return new WaitForEndOfFrame();
        
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
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }
    }

    private void Start()
    {
        _isMasterInstance = PhotonNetwork.IsMasterClient;
        
        if (_echoPresetLocal != null)
        {
            _echoPreset = _echoPresetLocal;
        }
    }

    private void Update()
    {
        if (!_isReady || !_isMasterInstance) return;

        if (Time.time - _spawnTime >= _maxLifetime)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (_isReady && _isMasterInstance)
        {
            _previousPosition = transform.position;
            _hasPreviousPosition = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_isReady || !_isMasterInstance) return;

        Vector3 echoPosition = GetEchoSpawnPosition(collision);
        
        SpawnEchoAtCollision(echoPosition);

        _currentCollisions++;

        if (_currentCollisions >= _maxCollisions)
        {
            SpawnFinalEcho(echoPosition);
            PhotonNetwork.Destroy(gameObject);
        }
    }

    private Vector3 GetEchoSpawnPosition(Collision collision)
    {
        Vector3 contactPoint = collision.contacts[0].point;
        Vector3 contactNormal = collision.contacts[0].normal;
        
        bool isFloorCollision = Vector3.Dot(contactNormal, Vector3.up) > 0.7f;
        
        if (isFloorCollision && _hasPreviousPosition)
        {
            Vector3 echoPos = _previousPosition;
            
            if (echoPos.y < contactPoint.y + _minEchoHeight)
            {
                echoPos.y = contactPoint.y + _minEchoHeight;
            }
            
            return echoPos;
        }
        
        return contactPoint + contactNormal * 0.1f;
    }

    private void SpawnEchoAtCollision(Vector3 position)
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(position, _echoPreset);
    }

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

using UnityEngine;
using Mirror;
using System.Collections;

/// <summary>
/// ��������� �����, ������� ������ ��� ��� ������������ � ���������.
/// ������������ ����� ������������ ���������� ������������.
/// ���������������� �� ���� ��� ���� �������.
/// </summary>
public class EchoBolt : NetworkBehaviour
{
    [Header("��������� ���")]
    [Tooltip("������ ��� ��� �����")]
    [SerializeField] private EchoPreset _echoPresetLocal;

    [Header("��������� ��������")]
    [Tooltip("������������ ���������� ������������ �� �����������")]
    [SyncVar]
    private int _maxCollisions = 3;

    [SyncVar]
    private int _currentCollisions;

    [Header("����� �����")]
    [Tooltip("������������ ����� ����� ����� (�������)")]
    [SyncVar]
    private float _maxLifetime = 10f;

    [SyncVar]
    private float _spawnTime;

    [Header("��������� ������� ���")]
    [Tooltip("����������� ������ ��� ������ ������������ ��� ������ ���")]
    [SerializeField] private float _minEchoHeight = 0.3f;

    private EchoPreset _echoPreset;
    private Rigidbody _rigidbody;
    private bool _isReady;
    private bool _isServerInstance;
    
    // ������� � ���������� �����
    private Vector3 _previousPosition;
    private bool _hasPreviousPosition;
    
    // ����������� ��������� ������ ��� ����������� ����������
    private Vector3 _pendingVelocity;
    private Vector3 _pendingAngularVelocity;
    private bool _hasPendingForce;

    /// <summary>
    /// �������������� ���� � ��������� �����������.
    /// ���������� �� ������� ����� ������.
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
        
        // ��������� ��������� ������
        _pendingVelocity = velocity;
        _pendingAngularVelocity = angularVelocity;
        _hasPendingForce = true;
        
        // �������������� ���������� �������
        _previousPosition = transform.position;
        _hasPreviousPosition = true;
        
        // ��������� ���������� ���������
        StartCoroutine(DelayedActivation());
    }

    private IEnumerator DelayedActivation()
    {
        // ��� ����� ����� ����� Spawn ��������� ����������
        yield return new WaitForEndOfFrame();
        
        // ������ ��������� ������
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
        
        // ����� ������ ��������������
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
        // �������������� ������ �� ������� �� ���������������� ����
        if (_echoPresetLocal != null)
        {
            _echoPreset = _echoPresetLocal;
        }
    }

    private void Update()
    {
        // ��������� ���������� ����� ������ ����������
        if (!_isReady || !_isServerInstance) return;

        // �������� ������� ����� (������ �� �������)
        if (Time.time - _spawnTime >= _maxLifetime)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    private void FixedUpdate()
    {
        // ��������� ������� ��� ���������� ����� (������ �� �������)
        if (_isReady && _isServerInstance)
        {
            _previousPosition = transform.position;
            _hasPreviousPosition = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // ��������� ����������
        if (!_isReady || !_isServerInstance) return;

        // ���������� ������� ��� ������ ���
        Vector3 echoPosition = GetEchoSpawnPosition(collision);
        
        // ������ ���
        SpawnEchoAtCollision(echoPosition);

        _currentCollisions++;

        if (_currentCollisions >= _maxCollisions)
        {
            // ��������� ��� ����� ������������
            SpawnFinalEcho(echoPosition);
            NetworkServer.Destroy(gameObject);
        }
    }

    /// <summary>
    /// ���������� ������� ��� ������ ���.
    /// ���������� ���������� ������� ���� �������� � ����� (������� ���������� �����).
    /// </summary>
    private Vector3 GetEchoSpawnPosition(Collision collision)
    {
        Vector3 contactPoint = collision.contacts[0].point;
        Vector3 contactNormal = collision.contacts[0].normal;
        
        // ���������, ��� �������� � ����� (������� ���������� �����)?
        bool isFloorCollision = Vector3.Dot(contactNormal, Vector3.up) > 0.7f;
        
        if (isFloorCollision && _hasPreviousPosition)
        {
            // ���������� ���������� �������, �� �� ���� ����������� ������ ��� ������ ��������
            Vector3 echoPos = _previousPosition;
            
            // ���������� ��� ��� �� ���� ����������� ������
            if (echoPos.y < contactPoint.y + _minEchoHeight)
            {
                echoPos.y = contactPoint.y + _minEchoHeight;
            }
            
            return echoPos;
        }
        
        // ��� ���� � �������� ���������� ����� �������� �� ��������� �� �������
        return contactPoint + contactNormal * 0.1f;
    }

    /// <summary>
    /// ������ ��� � ����� ������������.
    /// </summary>
    private void SpawnEchoAtCollision(Vector3 position)
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(position, _echoPreset);
    }

    /// <summary>
    /// ������ ��������� ��������� ��� ����� ������������.
    /// </summary>
    private void SpawnFinalEcho(Vector3 position)
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(
            position,
            _echoPreset,
            1.5f, 2f, 1.5f
        );
    }
}

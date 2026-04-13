using UnityEngine;

/// <summary>
/// ��������� ������ ����� ��� �����������.
/// </summary>
public class LocalEchoBolt : MonoBehaviour
{
    [Header("��������� ������� ���")]
    [Tooltip("����������� ������ ��� ������ ������������ ��� ������ ���")]
    [SerializeField] private float _minEchoHeight = 0.3f;

    private EchoPreset _echoPreset;
    private int _maxCollisions;
    private float _maxLifetime;
    private int _currentCollisions;
    private float _spawnTime;
    private bool _initialized;
    
    // ������� � ���������� �����
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
        
        // �������������� ���������� �������
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
        // ��������� ������� ��� ���������� �����
        if (_initialized)
        {
            _previousPosition = transform.position;
            _hasPreviousPosition = true;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_initialized) return;

        // ���������� ������� ��� ������ ���
        Vector3 echoPosition = GetEchoSpawnPosition(collision);

        // ������ ���
        if (_echoPreset != null && EchoManager.Instance != null)
        {
            EchoManager.Instance.SpawnEcho(echoPosition, _echoPreset);
        }

        _currentCollisions++;

        if (_currentCollisions >= _maxCollisions)
        {
            // ��������� ��������� ���
            if (_echoPreset != null && EchoManager.Instance != null)
            {
                EchoManager.Instance.SpawnEcho(
                    echoPosition,
                    _echoPreset,
                    1.5f, 2f, 1.5f
                );
            }
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ���������� ������� ��� ������ ���.
    /// ���������� ���������� ������� ���� �������� � �����.
    /// </summary>
    private Vector3 GetEchoSpawnPosition(Collision collision)
    {
        Vector3 contactPoint = collision.contacts[0].point;
        Vector3 contactNormal = collision.contacts[0].normal;
        
        // ���������, ��� �������� � ����� (������� ���������� �����)?
        bool isFloorCollision = Vector3.Dot(contactNormal, Vector3.up) > 0.7f;
        
        if (isFloorCollision && _hasPreviousPosition)
        {
            // ���������� ���������� �������
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
}

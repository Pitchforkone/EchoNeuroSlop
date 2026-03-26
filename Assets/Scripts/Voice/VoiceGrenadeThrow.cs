using System;
using UnityEngine;
using Mirror;

/// <summary>
/// ��������� ��� ������ ���-������� ��������� ��������.
/// ������� ����� "Grenade" � ������� ������ �� ���� � ����������� �������.
/// ������� ������������ ������ (������ ���) �� ����� �����.
/// 
/// �������������:
///   1. �������� �� ������ ������.
///   2. ��������� EchoPreset ��� ������� �������.
///   3. ����������� ��������� ��������� ������.
/// </summary>
public class VoiceGrenadeThrow : NetworkBehaviour, IVoiceWordListener
{
    [Header("��������� �������")]
    [Tooltip("����� ��� ��������� ������ �������")]
    [SerializeField] private string _throwWord = "grenade";

    [Header("��������� ������")]
    [Tooltip("���� ������")]
    [SerializeField] private float _throwForce = 15f;

    [Tooltip("���� ������ ����� (�������)")]
    [SerializeField] private float _throwAngle = 30f;

    [Tooltip("����� ������ ������� ������������ ������")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 1.5f, 0.5f);

    [Header("��������� �������")]
    [Tooltip("������ ������� (�����������, ���� null - ��������� �����)")]
    [SerializeField] private GameObject _grenadePrefab;

    [Tooltip("������ �������")]
    [SerializeField] private float _grenadeSize = 0.15f;

    [Tooltip("����� ����� ������� (�������)")]
    [SerializeField] private float _grenadeLifetime = 5f;

    [Header("��������� ���-�������")]
    [Tooltip("������ ��� ��� �������")]
    [SerializeField] private EchoPreset _echoPreset;

    [Tooltip("�������� ����� ��������� (�������)")]
    [SerializeField] private float _blinkInterval = 0.3f;

    [Header("�������")]
    [Tooltip("����� ����������� ����� �������� (�������)")]
    [SerializeField] private float _cooldown = 2f;

    private VoiceRecognizer _voiceRecognizer;
    private Camera _playerCamera;
    private float _lastThrowTime = -999f;

    private void Start()
    {
        // ��� �����������
        if (!NetworkClient.active)
        {
            InitializeLocal();
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        InitializeLocal();
    }

    private void InitializeLocal()
    {
        // ���� VoiceRecognizer
        _voiceRecognizer = GetComponent<VoiceRecognizer>();
        if (_voiceRecognizer == null)
        {
            _voiceRecognizer = VoiceRecognizer.LocalInstance;
        }

        if (_voiceRecognizer != null)
        {
            _voiceRecognizer.AddListener(this);
        }
        else
        {
            Debug.LogWarning("[VoiceGrenadeThrow] VoiceRecognizer �� ������.");
        }

        // ���� ������ ������
        _playerCamera = GetComponentInChildren<Camera>();
        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }
    }

    public void OnWordRecognized(string word)
    {
        if (string.Equals(word, _throwWord, StringComparison.OrdinalIgnoreCase))
        {
            TryThrowGrenade();
        }
    }

    /// <summary>
    /// ������� ������� ������� � ��������� ��������.
    /// </summary>
    public void TryThrowGrenade()
    {
        if (Time.time - _lastThrowTime < _cooldown)
        {
            Debug.Log($"[VoiceGrenadeThrow] �����������: {_cooldown - (Time.time - _lastThrowTime):F1}�");
            return;
        }

        ThrowGrenade();
        _lastThrowTime = Time.time;
    }

    /// <summary>
    /// ������� ������� � ����������� �������.
    /// </summary>
    private void ThrowGrenade()
    {
        Vector3 spawnPosition = transform.TransformPoint(_spawnOffset);
        Vector3 throwDirection = GetThrowDirection();

        // ������ �������
        GameObject grenade = CreateGrenade(spawnPosition);

        // ��������� Rigidbody ���� ��� ���
        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = grenade.AddComponent<Rigidbody>();
        }

        rb.mass = 0.3f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // ��������� Collider ���� ��� ���
        if (grenade.GetComponent<Collider>() == null)
        {
            SphereCollider collider = grenade.AddComponent<SphereCollider>();
            collider.radius = _grenadeSize * 0.5f;
        }

        // ��������� ���� ������
        rb.linearVelocity = throwDirection * _throwForce;

        // ��������� �������� ��� ��������������
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;

        // ��������� ��������� �������
        EchoGrenade echoComponent = grenade.AddComponent<EchoGrenade>();
        echoComponent.Initialize(_echoPreset, _blinkInterval, _grenadeLifetime);

        Debug.Log("[VoiceGrenadeThrow] ������� �������!");
    }

    /// <summary>
    /// ��������� ����������� ������ � ������ ����.
    /// </summary>
    private Vector3 GetThrowDirection()
    {
        Vector3 forward;

        if (_playerCamera != null)
        {
            forward = _playerCamera.transform.forward;
        }
        else
        {
            forward = transform.forward;
        }

        // ������������ ������ ����� �� �������� ����
        Quaternion upRotation = Quaternion.AngleAxis(-_throwAngle, _playerCamera != null 
            ? _playerCamera.transform.right 
            : transform.right);

        return (upRotation * forward).normalized;
    }

    /// <summary>
    /// ������ ������ �������.
    /// </summary>
    private GameObject CreateGrenade(Vector3 position)
    {
        GameObject grenade;

        if (_grenadePrefab != null)
        {
            grenade = Instantiate(_grenadePrefab, position, Quaternion.identity);
        }
        else
        {
            // ������ ������� �����
            grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenade.name = "EchoGrenade";
            grenade.transform.position = position;
            grenade.transform.localScale = Vector3.one * _grenadeSize;

            // ������ � �����
            Renderer renderer = grenade.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.1f, 0.1f, 0.15f);
                mat.SetFloat("_Metallic", 0.8f);
                mat.SetFloat("_Glossiness", 0.6f);
                renderer.material = mat;
            }
        }

        return grenade;
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_voiceRecognizer != null)
        {
            _voiceRecognizer.RemoveListener(this);
            _voiceRecognizer = null;
        }
    }
}

/// <summary>
/// ��������� �������, ������� ������������ ������ ���-�������.
/// </summary>
public class EchoGrenade : MonoBehaviour
{
    private EchoPreset _echoPreset;
    private float _blinkInterval;
    private float _lifetime;

    private float _blinkTimer;
    private float _lifeTimer;
    private bool _initialized;


    /// <summary>
    /// Инициализирует гранату.
    /// </summary>
    public void Initialize(EchoPreset echoPreset, float blinkInterval, float lifetime)
    {
        _echoPreset = echoPreset;
        _blinkInterval = blinkInterval;
        _lifetime = lifetime;

        _blinkTimer = 0f;
        _lifeTimer = lifetime;
        _initialized = true;

        // Первая вспышка сразу
        SpawnEcho();
    }

    private void Update()
    {
        if (!_initialized) return;

        // Обратный отсчёт времени жизни
        _lifeTimer -= Time.deltaTime;
        if (_lifeTimer <= 0f)
        {
            // Финальная мощная вспышка перед уничтожением
            SpawnFinalEcho();
            Destroy(gameObject);
            return;
        }

        // Таймер вспышек
        _blinkTimer -= Time.deltaTime;
        if (_blinkTimer <= 0f)
        {
            SpawnEcho();
            _blinkTimer = _blinkInterval;
        }
    }

    /// <summary>
    /// Обработка столкновения с объектами.
    /// При касании объектов (не Ground) создаётся вспышка.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (!_initialized) return;

        // Если столкнулись не с землёй — создаём вспышк
            SpawnEcho();
    }

    /// <summary>
    /// Создаёт эхо-вспышку.
    /// </summary>
    private void SpawnEcho()
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(transform.position, _echoPreset);
    }

    /// <summary>
    /// Создаёт финальную усиленную вспышку.
    /// </summary>
    private void SpawnFinalEcho()
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        // ��������� ��������� �������
        EchoManager.Instance.SpawnEcho(
            transform.position,
            _echoPreset.Speed,
            _echoPreset.MaxRadius * 1.5f,
            _echoPreset.Intensity * 2f,
            _echoPreset.Color,
            _echoPreset.Lifetime * 1.5f
        );
    }
}

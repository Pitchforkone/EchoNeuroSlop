using System;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Компонент для броска эхо-гранаты из инвентаря.
/// Слушает событие GrenadeItem.OnGrenadeUsed и бросает гранату.
/// </summary>
public class VoiceGrenadeThrow : MonoBehaviourPun
{
    [Header("Настройки броска")]
    [SerializeField] private float _throwForce = 15f;
    [SerializeField] private float _throwAngle = 30f;
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 1.5f, 0.5f);

    [Header("Настройки гранаты")]
    [SerializeField] private GameObject _grenadePrefab;
    [SerializeField] private float _grenadeLifetime = 5f;

    [Header("Настройки эхо-эффекта")]
    [SerializeField] private EchoPreset _echoPreset;
    [SerializeField] private float _blinkInterval = 0.3f;

    [Header("Кулдаун")]
    [SerializeField] private float _cooldown = 0.5f;

    private Camera _playerCamera;
    private float _lastThrowTime = -999f;
    private bool _isInitialized = false;

    private void Start()
    {
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            InitializeLocal();
        }
    }

    private void InitializeLocal()
    {
        if (_isInitialized) return;
        
        GrenadeItem.OnGrenadeUsed += OnGrenadeUsedFromInventory;
        
        _playerCamera = GetComponentInChildren<Camera>();
        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }
        
        _isInitialized = true;
        Debug.Log("[VoiceGrenadeThrow] Initialized and listening for grenade use");
    }

    private void OnGrenadeUsedFromInventory(int remainingCount)
    {
        if (Time.time - _lastThrowTime < _cooldown)
        {
            Debug.Log($"[VoiceGrenadeThrow] Cooldown: {_cooldown - (Time.time - _lastThrowTime):F1}s");
            return;
        }

        ThrowGrenade();
        _lastThrowTime = Time.time;
        
        Debug.Log($"[VoiceGrenadeThrow] Grenade thrown! Remaining in inventory: {remainingCount}");
    }

    private void ThrowGrenade()
    {
        Vector3 spawnPosition = transform.TransformPoint(_spawnOffset);
        Vector3 throwDirection = GetThrowDirection();

        GameObject grenade = CreateGrenade(spawnPosition);

        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = grenade.AddComponent<Rigidbody>();
        }

        rb.mass = 0.3f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.linearVelocity = throwDirection * _throwForce;
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;

        EchoGrenade echoComponent = grenade.AddComponent<EchoGrenade>();
        echoComponent.Initialize(_echoPreset, _blinkInterval, _grenadeLifetime);
    }

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

        Quaternion upRotation = Quaternion.AngleAxis(-_throwAngle, _playerCamera != null 
            ? _playerCamera.transform.right 
            : transform.right);

        return (upRotation * forward).normalized;
    }

    private GameObject CreateGrenade(Vector3 position)
    {
        GameObject grenade;

        if (_grenadePrefab != null)
        {
            grenade = Instantiate(_grenadePrefab, position, Quaternion.identity);
        }
        else
        {
            grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenade.name = "EchoGrenade";
            grenade.transform.position = position;
            grenade.transform.localScale = Vector3.one * 0.3f;

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
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (_isInitialized)
        {
            GrenadeItem.OnGrenadeUsed -= OnGrenadeUsedFromInventory;
            _isInitialized = false;
        }
    }
}

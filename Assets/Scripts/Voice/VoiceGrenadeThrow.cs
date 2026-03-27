using System;
using UnityEngine;
using Mirror;

/// <summary>
/// Компонент для броска эхо-гранаты из инвентаря.
/// Слушает событие GrenadeItem.OnGrenadeUsed и бросает гранату.
/// Гранаты подбираются как предметы и хранятся в PlayerInventory.
/// 
/// Использование:
///   1. Подберите гранату (PickupableItem с типом Grenade).
///   2. Скажите "grenade" для броска.
///   3. Граната будет взята из инвентаря.
/// </summary>
public class VoiceGrenadeThrow : NetworkBehaviour
{
    [Header("Настройки броска")]
    [Tooltip("Сила броска")]
    [SerializeField] private float _throwForce = 15f;

    [Tooltip("Угол броска вверх (градусы)")]
    [SerializeField] private float _throwAngle = 30f;

    [Tooltip("Точка спавна гранаты относительно игрока")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 1.5f, 0.5f);

    [Header("Настройки гранаты")]
    [Tooltip("Префаб гранаты (опционально, если null - создается сфера)")]
    [SerializeField] private GameObject _grenadePrefab;

    [Tooltip("Время жизни гранаты (секунды)")]
    [SerializeField] private float _grenadeLifetime = 5f;

    [Header("Настройки эхо-эффекта")]
    [Tooltip("Пресет эхо для гранаты")]
    [SerializeField] private EchoPreset _echoPreset;

    [Tooltip("Интервал между вспышками (секунды)")]
    [SerializeField] private float _blinkInterval = 0.3f;

    [Header("Кулдаун")]
    [Tooltip("Время перезарядки между бросками (секунды)")]
    [SerializeField] private float _cooldown = 0.5f;

    private Camera _playerCamera;
    private float _lastThrowTime = -999f;
    private bool _isInitialized = false;

    private void Start()
    {
        // Для оффлайн-режима
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
        if (_isInitialized) return;
        
        // Подписываемся на событие использования гранаты
        GrenadeItem.OnGrenadeUsed += OnGrenadeUsedFromInventory;
        
        // Ищем камеру игрока
        _playerCamera = GetComponentInChildren<Camera>();
        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }
        
        _isInitialized = true;
        Debug.Log("[VoiceGrenadeThrow] Initialized and listening for grenade use");
    }

    /// <summary>
    /// Вызывается когда GrenadeItem используется через голосовую команду.
    /// </summary>
    private void OnGrenadeUsedFromInventory(int remainingCount)
    {
        // Проверяем кулдаун
        if (Time.time - _lastThrowTime < _cooldown)
        {
            Debug.Log($"[VoiceGrenadeThrow] Cooldown: {_cooldown - (Time.time - _lastThrowTime):F1}s");
            return;
        }

        ThrowGrenade();
        _lastThrowTime = Time.time;
        
        Debug.Log($"[VoiceGrenadeThrow] Grenade thrown! Remaining in inventory: {remainingCount}");
    }

    /// <summary>
    /// Бросает гранату в направлении взгляда.
    /// </summary>
    private void ThrowGrenade()
    {
        Vector3 spawnPosition = transform.TransformPoint(_spawnOffset);
        Vector3 throwDirection = GetThrowDirection();

        // Создаём гранату
        GameObject grenade = CreateGrenade(spawnPosition);

        // Настройка Rigidbody если его нет
        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = grenade.AddComponent<Rigidbody>();
        }

        rb.mass = 0.3f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Применяем силу броска
        rb.linearVelocity = throwDirection * _throwForce;

        // Добавляем вращение для реалистичности
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;

        // Добавляем компонент гранаты
        EchoGrenade echoComponent = grenade.AddComponent<EchoGrenade>();
        echoComponent.Initialize(_echoPreset, _blinkInterval, _grenadeLifetime);
    }

    /// <summary>
    /// Вычисляет направление броска с учётом угла.
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

        // Поворачиваем вектор вверх на заданный угол
        Quaternion upRotation = Quaternion.AngleAxis(-_throwAngle, _playerCamera != null 
            ? _playerCamera.transform.right 
            : transform.right);

        return (upRotation * forward).normalized;
    }

    /// <summary>
    /// Создаёт объект гранаты.
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
            // Создаём простую сферу
            grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenade.name = "EchoGrenade";
            grenade.transform.position = position;
            grenade.transform.localScale = Vector3.one * 0.3f;

            // Настройка материала
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


using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// Компонент для броска болта.
/// Устанавливается на игрока, подписывается на правую кнопку мыши.
/// Бросает болт, который создаёт эхо при столкновениях.
/// Болт виден всем игрокам через сетевую синхронизацию.
/// 
/// ВАЖНО: Префаб болта должен быть добавлен в Registered Spawnable Prefabs в NetworkManager!
// Синхронизация параметров через сеть не требуется, так как используется только локальный ввод
/// 
/// Использование:
///   1. Добавьте на объект игрока.
///   2. Назначьте EchoPreset для визуальных эффектов.
///   3. Создайте префаб болта с компонентами: EchoBolt, NetworkIdentity, Rigidbody, SphereCollider.
///   4. Добавьте префаб в NetworkManager -> Registered Spawnable Prefabs.
///   5. Назначьте префаб в поле Bolt Prefab.
/// </summary>
public class BoltThrower : NetworkBehaviour
{
    [Header("Настройки ввода")]
    [Tooltip("Input Action Asset для управления")]
    [SerializeField] private InputActionAsset _inputActions;

    [Tooltip("Имя action map")]
    [SerializeField] private string _actionMapName = "Player";

    [Tooltip("Имя действия для броска болта")]
    [SerializeField] private string _throwActionName = "ThrowBolt";

    [Header("Настройки броска")]
    [Tooltip("Сила броска")]
    [SerializeField] private float _throwForce = 20f;

    [Tooltip("Угол броска вверх (градусы)")]
    [SerializeField] private float _throwAngle = 15f;

    [Tooltip("Смещение точки спавна болта относительно игрока")]
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 1.5f, 0.5f);

    [Header("Настройки болта")]
    [Tooltip("Префаб болта (ОБЯЗАТЕЛЬНО для мультиплеера! Должен быть в NetworkManager Spawnable Prefabs)")]
    [SerializeField] private GameObject _boltPrefab;

    [Tooltip("Максимальное количество столкновений до уничтожения")]
    [SerializeField] private int _maxCollisions = 3;

    [Tooltip("Максимальное время жизни болта (секунды)")]
    [SerializeField] private float _boltLifetime = 10f;

    [Header("Настройки эхо")]
    [Tooltip("Пресет эхо для болта")]
    [SerializeField] private EchoPreset _echoPreset;

    [Header("Перезарядка")]
    [Tooltip("Время перезарядки между бросками (секунды)")]
    [SerializeField] private float _cooldown = 1.5f;

    private Camera _playerCamera;
    private float _lastThrowTime = -999f;
    private bool _isInitialized;
    private InputAction _throwAction;
    private static bool _prefabRegistered;

    private void Start()
    {
        // Регистрируем префаб в NetworkManager (один раз)
        RegisterPrefabIfNeeded();
        
        // Для синглплеера
        if (!NetworkClient.active)
        {
            InitializeLocal();
        }
    }

    /// <summary>
    /// Регистрирует префаб болта в NetworkManager, если ещё не зарегистрирован.
    /// </summary>
    private void RegisterPrefabIfNeeded()
    {
        if (_prefabRegistered || _boltPrefab == null) return;
        
        // Проверяем есть ли NetworkIdentity
        if (_boltPrefab.GetComponent<NetworkIdentity>() == null)
        {
            Debug.LogError("[BoltThrower] Bolt prefab must have a NetworkIdentity component!");
            return;
        }

        // Регистрируем префаб для спавна
        if (!NetworkClient.prefabs.ContainsValue(_boltPrefab))
        {
            NetworkClient.RegisterPrefab(_boltPrefab);
            Debug.Log($"[BoltThrower] Registered bolt prefab: {_boltPrefab.name}");
        }
        
        _prefabRegistered = true;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        InitializeLocal();
    }

    private void InitializeLocal()
    {
        if (_isInitialized) return;

        // Проверка префаба для мультиплеера
        if (NetworkClient.active && _boltPrefab == null)
        {
            Debug.LogError("[BoltThrower] Bolt Prefab is required for multiplayer! Please assign a prefab with NetworkIdentity.");
        }

        // Ищем камеру игрока
        _playerCamera = GetComponentInChildren<Camera>();
        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }

        // Настраиваем Input System
        SetupInputActions();

        _isInitialized = true;
    }

    private void SetupInputActions()
    {
        if (_inputActions != null)
        {
            var actionMap = _inputActions.FindActionMap(_actionMapName);
            if (actionMap != null)
            {
                _throwAction = actionMap.FindAction(_throwActionName);
            }
        }

        // Если action не найден, создаём default action для правой кнопки мыши
        if (_throwAction == null)
        {
            _throwAction = new InputAction("ThrowBolt", InputActionType.Button, "<Mouse>/rightButton");
        }

        _throwAction.Enable();
        _throwAction.performed += OnThrowActionPerformed;
    }

    private void OnThrowActionPerformed(InputAction.CallbackContext context)
    {
        if (!_isInitialized) return;
        if (NetworkClient.active && !isLocalPlayer) return;

        TryThrowBolt();
    }

    private void OnDisable()
    {
        CleanupInputActions();
    }

    private void OnDestroy()
    {
        CleanupInputActions();
    }

    private void CleanupInputActions()
    {
        if (_throwAction != null)
        {
            _throwAction.performed -= OnThrowActionPerformed;
            _throwAction.Disable();
            _throwAction = null;
        }
    }

    /// <summary>
    /// Попытка бросить болт с проверкой перезарядки.
    /// </summary>
    public void TryThrowBolt()
    {
        if (Time.time - _lastThrowTime < _cooldown)
        {
            float remaining = _cooldown - (Time.time - _lastThrowTime);
            return;
        }

        // Проверка префаба для мультиплеера
        if (NetworkClient.active && _boltPrefab == null)
        {
            Debug.LogError("[BoltThrower] Cannot throw bolt - prefab not assigned!");
            return;
        }

        _lastThrowTime = Time.time;

        // Вычисляем позицию и направление броска
        Vector3 spawnPosition = transform.TransformPoint(_spawnOffset);
        Vector3 throwDirection = GetThrowDirection();
        Vector3 velocity = throwDirection * _throwForce;
        Vector3 angularVelocity = Random.insideUnitSphere * 5f;

        // Отправляем команду на сервер для спавна болта
        if (NetworkClient.active)
        {
            CmdThrowBolt(spawnPosition, velocity, angularVelocity);
        }
        else
        {
            // Синглплеер - создаём локально
            SpawnBoltLocal(spawnPosition, velocity, angularVelocity);
        }
    }

    /// <summary>
    /// Команда серверу для создания болта.
    /// </summary>
    [Command]
    private void CmdThrowBolt(Vector3 spawnPosition, Vector3 velocity, Vector3 angularVelocity)
    {
        SpawnBoltOnServer(spawnPosition, velocity, angularVelocity);
    }

    /// <summary>
    /// Создаёт болт на сервере и синхронизирует на всех клиентах.
    /// </summary>
    [Server]
    private void SpawnBoltOnServer(Vector3 spawnPosition, Vector3 velocity, Vector3 angularVelocity)
    {
        if (_boltPrefab == null)
        {
            Debug.LogError("[BoltThrower] Cannot spawn bolt on server - prefab not assigned!");
            return;
        }

        // Создаём болт из префаба
        GameObject bolt = Instantiate(_boltPrefab, spawnPosition, Quaternion.identity);
        
        // Игнорируем коллизии болта с игроком
        Collider boltCollider = bolt.GetComponent<Collider>();
        Collider playerCollider = GetComponent<Collider>();
        if (boltCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(boltCollider, playerCollider);
        }
        
        // Также игнорируем коллизии с CharacterController
        CharacterController characterController = GetComponent<CharacterController>();
        if (boltCollider != null && characterController != null)
        {
            Physics.IgnoreCollision(boltCollider, characterController);
        }

        // Настраиваем Rigidbody (будет кинематическим, EchoBolt сам включит физику)
        Rigidbody rb = bolt.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // EchoBolt включит физику после инициализации
        }

        // Получаем компонент EchoBolt
        EchoBolt echoBolt = bolt.GetComponent<EchoBolt>();
        if (echoBolt == null)
        {
            Debug.LogError("[BoltThrower] Bolt prefab must have EchoBolt component!");
            Destroy(bolt);
            return;
        }

        // Спавним объект в сети (будет виден всем игрокам)
        NetworkServer.Spawn(bolt);

        // Инициализируем болт после спавна (передаём velocity для отложенного применения)
        echoBolt.Initialize(_echoPreset, _maxCollisions, _boltLifetime, velocity, angularVelocity);
    }

    /// <summary>
    /// Создаёт болт локально (для синглплеера).
    /// </summary>
    private void SpawnBoltLocal(Vector3 spawnPosition, Vector3 velocity, Vector3 angularVelocity)
    {
        GameObject bolt;
        
        if (_boltPrefab != null)
        {
            bolt = Instantiate(_boltPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // Создаём простой болт-сферу для синглплеера
            bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "EchoBolt";
            bolt.transform.position = spawnPosition;
            bolt.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            // Тёмный металлический материал
            Renderer renderer = bolt.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.15f, 0.15f, 0.2f);
                mat.SetFloat("_Metallic", 0.9f);
                mat.SetFloat("_Glossiness", 0.7f);
                renderer.material = mat;
            }
            
            // Добавляем Rigidbody
            Rigidbody rb = bolt.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        // Игнорируем коллизии болта с игроком
        Collider boltCollider = bolt.GetComponent<Collider>();
        Collider playerCollider = GetComponent<Collider>();
        if (boltCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(boltCollider, playerCollider);
        }
        
        // Также игнорируем коллизии с CharacterController
        CharacterController characterController = GetComponent<CharacterController>();
        if (boltCollider != null && characterController != null)
        {
            Physics.IgnoreCollision(boltCollider, characterController);
        }

        // Настраиваем Rigidbody
        Rigidbody boltRb = bolt.GetComponent<Rigidbody>();
        if (boltRb != null)
        {
            boltRb.linearVelocity = velocity;
            boltRb.angularVelocity = angularVelocity;
        }

        // Добавляем логику эхо для синглплеера
        LocalEchoBolt localBolt = bolt.GetComponent<LocalEchoBolt>();
        if (localBolt == null)
        {
            localBolt = bolt.AddComponent<LocalEchoBolt>();
        }
        localBolt.Initialize(_echoPreset, _maxCollisions, _boltLifetime);
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
    /// Возвращает оставшееся время перезарядки.
    /// </summary>
    public float GetCooldownRemaining()
    {
        float remaining = _cooldown - (Time.time - _lastThrowTime);
        return remaining > 0f ? remaining : 0f;
    }

    /// <summary>
    /// Проверяет, готов ли болт к броску.
    /// </summary>
    public bool IsReady()
    {
        return Time.time - _lastThrowTime >= _cooldown;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Проверка в редакторе
        if (_boltPrefab != null)
        {
            if (_boltPrefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogWarning("[BoltThrower] Bolt prefab should have a NetworkIdentity component for multiplayer!");
            }
            if (_boltPrefab.GetComponent<EchoBolt>() == null)
            {
                Debug.LogWarning("[BoltThrower] Bolt prefab should have an EchoBolt component!");
            }
            if (_boltPrefab.GetComponent<Rigidbody>() == null)
            {
                Debug.LogWarning("[BoltThrower] Bolt prefab should have a Rigidbody component!");
            }
        }
    }
#endif
}



using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// Компонент для броска болта.
/// Устанавливается на игрока, подписывается на правую кнопку мыши.
/// Бросает болт, который создаёт эхо при столкновениях.
/// Болт виден всем игрокам через сетевую синхронизацию.
/// 
/// Использование:
///   1. Добавьте на объект игрока.
///   2. Назначьте EchoPreset для визуальных эффектов.
///   3. Создайте и назначьте префаб болта с компонентом EchoBolt и NetworkIdentity.
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
    [Tooltip("Префаб болта (должен иметь EchoBolt и NetworkIdentity)")]
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

    private void Start()
    {
        // Для синглплеера
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

    private void Update()
    {
        // Обрабатываем ввод только для локального игрока
        if (!_isInitialized) return;
        if (NetworkClient.active && !isLocalPlayer) return;
    }

    /// <summary>
    /// Попытка бросить болт с проверкой перезарядки.
    /// </summary>
    public void TryThrowBolt()
    {
        if (Time.time - _lastThrowTime < _cooldown)
        {
            float remaining = _cooldown - (Time.time - _lastThrowTime);
            Debug.Log($"[BoltThrower] Перезарядка: {remaining:F1}с");
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
        GameObject bolt = CreateBoltObject(spawnPosition, forNetwork: true);
        
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
        if (rb == null)
        {
            rb = bolt.AddComponent<Rigidbody>();
        }
        rb.mass = 0.2f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.isKinematic = true; // EchoBolt включит физику после инициализации

        // Получаем компонент EchoBolt
        EchoBolt echoBolt = bolt.GetComponent<EchoBolt>();
        if (echoBolt == null)
        {
            echoBolt = bolt.AddComponent<EchoBolt>();
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
        GameObject bolt = CreateBoltObject(spawnPosition, forNetwork: false);

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
        Rigidbody rb = bolt.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = bolt.AddComponent<Rigidbody>();
        }
        rb.mass = 0.2f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.linearVelocity = velocity;
        rb.angularVelocity = angularVelocity;

        // Добавляем простую логику эхо для синглплеера
        LocalEchoBolt localBolt = bolt.AddComponent<LocalEchoBolt>();
        localBolt.Initialize(_echoPreset, _maxCollisions, _boltLifetime);
        
        Debug.Log($"[BoltThrower] Болт создан локально. Collider: {boltCollider != null}, IsTrigger: {boltCollider?.isTrigger}");
    }

    /// <summary>
    /// Создаёт объект болта.
    /// </summary>
    private GameObject CreateBoltObject(Vector3 position, bool forNetwork = false)
    {
        GameObject bolt;

        if (_boltPrefab != null)
        {
            bolt = Instantiate(_boltPrefab, position, Quaternion.identity);
        }
        else
        {
            // Создаём простой болт-капсулу
            bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "EchoBolt";
            bolt.transform.position = position;
            bolt.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            // Убеждаемся что коллайдер не триггер
            Collider col = bolt.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }

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

            // Добавляем NetworkIdentity для сетевой синхронизации
            if (forNetwork)
            {
                bolt.AddComponent<NetworkIdentity>();
            }
        }

        return bolt;
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
}



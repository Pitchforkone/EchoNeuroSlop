using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

/// <summary>
/// Компонент для броска болтов.
/// Инициализируется на клиенте, бросок отправляется на MasterClient.
/// Префаб болта должен быть в папке Resources.
/// </summary>
public class BoltThrower : MonoBehaviourPun
{
    [Header("Настройки ввода")]
    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField] private string _actionMapName = "Player";
    [SerializeField] private string _throwActionName = "ThrowBolt";

    [Header("Настройки броска")]
    [SerializeField] private float _throwForce = 20f;
    [SerializeField] private float _throwAngle = 15f;
    [SerializeField] private Vector3 _spawnOffset = new Vector3(0f, 1.5f, 0.5f);

    [Header("Настройки болта")]
    [Tooltip("Имя префаба болта в папке Resources")]
    [SerializeField] private string _boltPrefabName = "EchoBolt";
    [Tooltip("Префаб болта для офлайн-режима")]
    [SerializeField] private GameObject _boltPrefab;
    [SerializeField] private int _maxCollisions = 3;
    [SerializeField] private float _boltLifetime = 10f;

    [Header("Настройки эхо")]
    [SerializeField] private EchoPreset _echoPreset;

    [Header("Перезарядка")]
    [SerializeField] private float _cooldown = 1.5f;

    private Camera _playerCamera;
    private float _lastThrowTime = -999f;
    private bool _isInitialized;
    private InputAction _throwAction;

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

        _playerCamera = GetComponentInChildren<Camera>();
        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }

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
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

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

    public void TryThrowBolt()
    {
        if (Time.time - _lastThrowTime < _cooldown)
        {
            return;
        }

        _lastThrowTime = Time.time;

        Vector3 spawnPosition = transform.TransformPoint(_spawnOffset);
        Vector3 throwDirection = GetThrowDirection();
        Vector3 velocity = throwDirection * _throwForce;
        Vector3 angularVelocity = Random.insideUnitSphere * 5f;

        if (PhotonNetwork.IsConnected)
        {
            // Spawn bolt via Photon on MasterClient
            photonView.RPC(nameof(RpcThrowBolt), RpcTarget.MasterClient, spawnPosition, velocity, angularVelocity);
        }
        else
        {
            SpawnBoltLocal(spawnPosition, velocity, angularVelocity);
        }
    }

    [PunRPC]
    private void RpcThrowBolt(Vector3 spawnPosition, Vector3 velocity, Vector3 angularVelocity)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        GameObject bolt = PhotonNetwork.Instantiate(_boltPrefabName, spawnPosition, Quaternion.identity);

        Collider boltCollider = bolt.GetComponent<Collider>();
        Collider playerCollider = GetComponent<Collider>();
        if (boltCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(boltCollider, playerCollider);
        }

        CharacterController characterController = GetComponent<CharacterController>();
        if (boltCollider != null && characterController != null)
        {
            Physics.IgnoreCollision(boltCollider, characterController);
        }

        EchoBolt echoBolt = bolt.GetComponent<EchoBolt>();
        if (echoBolt == null)
        {
            Debug.LogError("[BoltThrower] Bolt prefab must have EchoBolt component!");
            PhotonNetwork.Destroy(bolt);
            return;
        }

        echoBolt.Initialize(_echoPreset, _maxCollisions, _boltLifetime, velocity, angularVelocity);
    }

    private void SpawnBoltLocal(Vector3 spawnPosition, Vector3 velocity, Vector3 angularVelocity)
    {
        GameObject bolt;
        
        if (_boltPrefab != null)
        {
            bolt = Instantiate(_boltPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "EchoBolt";
            bolt.transform.position = spawnPosition;
            bolt.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

            Renderer renderer = bolt.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.15f, 0.15f, 0.2f);
                mat.SetFloat("_Metallic", 0.9f);
                mat.SetFloat("_Glossiness", 0.7f);
                renderer.material = mat;
            }
            
            Rigidbody rb = bolt.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        Collider boltCollider = bolt.GetComponent<Collider>();
        Collider playerCollider = GetComponent<Collider>();
        if (boltCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(boltCollider, playerCollider);
        }
        
        CharacterController characterController = GetComponent<CharacterController>();
        if (boltCollider != null && characterController != null)
        {
            Physics.IgnoreCollision(boltCollider, characterController);
        }

        Rigidbody boltRb = bolt.GetComponent<Rigidbody>();
        if (boltRb != null)
        {
            boltRb.linearVelocity = velocity;
            boltRb.angularVelocity = angularVelocity;
        }

        LocalEchoBolt localBolt = bolt.GetComponent<LocalEchoBolt>();
        if (localBolt == null)
        {
            localBolt = bolt.AddComponent<LocalEchoBolt>();
        }
        localBolt.Initialize(_echoPreset, _maxCollisions, _boltLifetime);
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

    public float GetCooldownRemaining()
    {
        float remaining = _cooldown - (Time.time - _lastThrowTime);
        return remaining > 0f ? remaining : 0f;
    }

    public bool IsReady()
    {
        return Time.time - _lastThrowTime >= _cooldown;
    }
}

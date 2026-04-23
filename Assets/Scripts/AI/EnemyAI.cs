using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Mirror;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NetworkIdentity))]
public class EnemyAI : NetworkBehaviour
{
    private enum EnemyState
    {
        Idle,
        Walking,
        Waiting,
        Chasing
    }

    [Header("Patrol Settings")]
    [SerializeField] private List<PatrolPoint> _patrolPoints = new List<PatrolPoint>();

    [SerializeField] private float _defaultWaitTime = 2f;

    [SerializeField] private float _patrolSpeed = 3.5f;

    [Header("Chase Settings")]
    [SerializeField] private float _chaseSpeed = 5f;

    [SerializeField] private float _chaseReachDistance = 1.5f;

    [SerializeField] private float _pathUpdateInterval = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip _chaseStartClip;

    [SerializeField] [Range(0f, 1f)] private float _chaseStartVolume = 1f;
    private Animator _animator;

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;

    [Header("Kill Settings")]
    [SerializeField] private string _playerTag = "Player";

    private NavMeshAgent _agent;
    private AudioSource _audioSource;

    [SyncVar(hook = nameof(OnStateChanged))]
    private EnemyState _currentState = EnemyState.Idle;
    
    private PatrolPoint _currentTarget;
    private PatrolPoint _previousTarget;
    private float _waitTimer;

    private Vector3 _chaseTargetPosition;
    private Transform _chaseTargetTransform;
    private float _chaseDuration;
    private float _chaseTimer;
    private int _currentChasePriority;
    private float _pathUpdateTimer;

    [SyncVar]
    private int _currentTargetIndex = -1;

    public float CurrentSpeed => _agent != null ? _agent.speed : 0f;

    public bool IsChasing => _currentState == EnemyState.Chasing;

    // Хэши параметров аниматора для оптимизации
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int WalkHash = Animator.StringToHash("Walk");

    private void OnStateChanged(EnemyState oldState, EnemyState newState)
    {
        UpdateAnimationState(newState);
    }

    private void UpdateAnimationState(EnemyState state)
    {
        if (_animator == null) return;

        switch (state)
        {
            case EnemyState.Idle:
            case EnemyState.Waiting:
                _animator.ResetTrigger(WalkHash);
                _animator.SetTrigger(IdleHash);
                break;

            case EnemyState.Walking:
            case EnemyState.Chasing:
                _animator.ResetTrigger(IdleHash);
                _animator.SetTrigger(WalkHash);
                break;
        }
    }

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.maxDistance = 30f;
            _audioSource.playOnAwake = false;
        }
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = _patrolSpeed;

        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }
        _patrolPoints = FindObjectsOfType<PatrolPoint>().ToList();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        if (_patrolPoints.Count == 0)
        {
            Debug.LogWarning($"[EnemyAI] No patrol points assigned to {gameObject.name}!", this);
            return;
        }

        SelectNextPatrolPoint();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        if (!isServer)
        {
            _agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!isServer) return;

        switch (_currentState)
        {
            case EnemyState.Walking:
                UpdateWalking();
                break;

            case EnemyState.Waiting:
                UpdateWaiting();
                break;

            case EnemyState.Chasing:
                UpdateChasing();
                break;

            case EnemyState.Idle:
                break;
        }
    }

    private void UpdateWalking()
    {
        if (_currentTarget == null)
        {
            SelectNextPatrolPoint();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
        
        if (distanceToTarget <= _currentTarget.ReachRadius || 
            (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance))
        {
            StartWaiting();
        }
    }

    private void UpdateWaiting()
    {
        _waitTimer -= Time.deltaTime;

        if (_waitTimer <= 0f)
        {
            SelectNextPatrolPoint();
        }
    }

    private void UpdateChasing()
    {
        _chaseTimer -= Time.deltaTime;

        if (_chaseTimer <= 0f)
        {
            EndChase();
            return;
        }

        if (_chaseTargetTransform != null)
        {
            _pathUpdateTimer -= Time.deltaTime;
            if (_pathUpdateTimer <= 0f)
            {
                _pathUpdateTimer = _pathUpdateInterval;
                _chaseTargetPosition = _chaseTargetTransform.position;
                _agent.SetDestination(_chaseTargetPosition);
            }
        }

        float distanceToTarget = Vector3.Distance(transform.position, _chaseTargetPosition);
        if (distanceToTarget <= _chaseReachDistance)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[EnemyAI] {gameObject.name} reached chase target");
            }

            if (_chaseTargetTransform == null)
            {
                EndChase();
            }
        }
    }

    [Server]
    private void StartWaiting()
    {
        _currentState = EnemyState.Waiting;
        _agent.isStopped = true;

        float waitTime = _currentTarget != null ? _currentTarget.WaitTime : _defaultWaitTime;
        _waitTimer = waitTime;

        if (_showDebugInfo)
        {
            Debug.Log($"[EnemyAI] {gameObject.name} reached point, waiting {waitTime}s");
        }
    }

    [Server]
    private void SelectNextPatrolPoint()
    {
        if (_patrolPoints.Count == 0)
        {
            _currentState = EnemyState.Idle;
            return;
        }

        PatrolPoint nextPoint = FindNearestPatrolPoint();

        if (nextPoint == null)
        {
            Debug.LogWarning($"[EnemyAI] {gameObject.name} couldn't find next patrol point!", this);
            _currentState = EnemyState.Idle;
            return;
        }

        _previousTarget = _currentTarget;
        _currentTarget = nextPoint;
        _currentTargetIndex = _patrolPoints.IndexOf(nextPoint);

        MoveToPatrolPoint(_currentTarget);
    }

    private PatrolPoint FindNearestPatrolPoint()
    {
        PatrolPoint nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var point in _patrolPoints)
        {
            if (point == null) continue;

            if (point == _previousTarget && _patrolPoints.Count > 1) continue;

            if (point == _currentTarget) continue;

            float distance = Vector3.Distance(transform.position, point.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = point;
            }
        }

        if (nearest == null && _previousTarget != null)
        {
            nearest = _previousTarget;
        }

        return nearest;
    }

    [Server]
    private void MoveToPatrolPoint(PatrolPoint target)
    {
        if (target == null) return;

        _agent.speed = _patrolSpeed;
        _agent.isStopped = false;
        _agent.SetDestination(target.transform.position);
        _currentState = EnemyState.Walking;

        if (_showDebugInfo)
        {
            Debug.Log($"[EnemyAI] {gameObject.name} moving to {target.name}");
        }
    }
    [Server]
    public void SetPursuitTarget(Vector3 position, float duration, int priority)
    {
        if (_currentState == EnemyState.Chasing && priority < _currentChasePriority)
        {
            return;
        }

        if (_currentState == EnemyState.Chasing)
        {
            _chaseTargetPosition = position;
            _chaseTargetTransform = null;
            _chaseTimer = duration;
            _currentChasePriority = priority;
            _agent.SetDestination(position);
            return;
        }

        StartChase(position, null, duration, priority);
    }

    /// <summary>
    /// ������������� ���� ������������� (���������� Transform).
    /// ���������� �����������.
    /// </summary>
    [Server]
    public void SetPursuitTransform(Transform target, float duration, int priority)
    {
        if (target == null) return;

        if (_currentState == EnemyState.Chasing && priority < _currentChasePriority)
        {
            return;
        }

        if (_currentState == EnemyState.Chasing)
        {
            _chaseTargetPosition = target.position;
            _chaseTargetTransform = target;
            _chaseTimer = duration;
            _currentChasePriority = priority;
            _agent.SetDestination(target.position);
            return;
        }

        StartChase(target.position, target, duration, priority);
    }

    /// <summary>
    /// ��������� ������ ������������� (��� ����������� ��������).
    /// </summary>
    [Server]
    public void RefreshPursuitTarget(Transform target, float duration, int priority)
    {
        // ������ ���� ��� ���������� ��� ���� ��� ��������� ����
        if (_currentState == EnemyState.Chasing)
        {
            if (_chaseTargetTransform == target || priority >= _currentChasePriority)
            {
                _chaseTimer = duration;
                _currentChasePriority = priority;
                
                if (target != null)
                {
                    _chaseTargetTransform = target;
                    _chaseTargetPosition = target.position;
                }
            }
        }
    }

    [Server]
    private void StartChase(Vector3 position, Transform target, float duration, int priority)
    {
        _currentState = EnemyState.Chasing;
        _chaseTargetPosition = position;
        _chaseTargetTransform = target;
        _chaseDuration = duration;
        _chaseTimer = duration;
        _currentChasePriority = priority;
        _pathUpdateTimer = 0f;

        _agent.speed = _chaseSpeed;
        _agent.isStopped = false;
        _agent.SetDestination(position);

        RpcPlayChaseSound();

        if (_showDebugInfo)
        {
            string targetName = target != null ? target.name : "position";
            Debug.Log($"[EnemyAI] {gameObject.name} started chasing {targetName} for {duration}s (priority: {priority})");
        }
    }

    [ClientRpc]
    private void RpcPlayChaseSound()
    {
        if (_chaseStartClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_chaseStartClip, _chaseStartVolume);
        }
    }

    [Server]
    private void EndChase()
    {
        _chaseTargetTransform = null;
        _currentChasePriority = 0;

        if (_showDebugInfo)
        {
            Debug.Log($"[EnemyAI] {gameObject.name} ended chase, resuming patrol");
        }

        // ������������ � ��������������
        SelectNextPatrolPoint();
    }

    /// <summary>
    /// ������������� ������������� �������������.
    /// </summary>
    [Server]
    public void CancelChase()
    {
        if (_currentState == EnemyState.Chasing)
        {
            EndChase();
        }
    }

    /// <summary>
    /// ��������� ����� �������������� � ������.
    /// </summary>
    [Server]
    public void AddPatrolPoint(PatrolPoint point)
    {
        if (point != null && !_patrolPoints.Contains(point))
        {
            _patrolPoints.Add(point);
        }
    }

    /// <summary>
    /// ������� ����� �������������� �� ������.
    /// </summary>
    [Server]
    public void RemovePatrolPoint(PatrolPoint point)
    {
        _patrolPoints.Remove(point);
    }

    /// <summary>
    /// ������������� ��������������.
    /// </summary>
    [Server]
    public void StopPatrol()
    {
        _currentState = EnemyState.Idle;
        _agent.isStopped = true;
    }

    /// <summary>
    /// ������������ ��������������.
    /// </summary>
    [Server]
    public void ResumePatrol()
    {
        if (_currentState == EnemyState.Idle)
        {
            SelectNextPatrolPoint();
        }
    }

    /// <summary>
    /// ������� �� ������� ��� ��������� ������� (���� �����).
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdStopPatrol()
    {
        StopPatrol();
    }

    /// <summary>
    /// ������� �� ������� ��� ������������� ������� (���� �����).
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdResumePatrol()
    {
        ResumePatrol();
    }

    private void OnDrawGizmosSelected()
    {
        if (_patrolPoints == null || _patrolPoints.Count == 0) return;

        // ������ ����� �� ����� � ������ ��������������
        Gizmos.color = Color.green;
        foreach (var point in _patrolPoints)
        {
            if (point != null)
            {
                Gizmos.DrawLine(transform.position, point.transform.position);
            }
        }

        // ������ ������� ���� �������
        if (_currentTarget != null && _currentState != EnemyState.Chasing)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
            Gizmos.DrawWireSphere(_currentTarget.transform.position, 0.3f);
        }

        // ������ ���� �������������
        if (_currentState == EnemyState.Chasing)
        {
            Gizmos.color = Color.red;
            Vector3 targetPos = _chaseTargetTransform != null ? _chaseTargetTransform.position : _chaseTargetPosition;
            Gizmos.DrawLine(transform.position, targetPos);
            Gizmos.DrawWireSphere(targetPos, _chaseReachDistance);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isServer) return;
        
        if (hit.gameObject.CompareTag(_playerTag))
        {
            HandlePlayerCaught(hit.gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isServer) return;
        
        if (collision.gameObject.CompareTag(_playerTag))
        {
            HandlePlayerCaught(collision.gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;
        
        if (other.CompareTag(_playerTag))
        {
            HandlePlayerCaught(other.gameObject);
        }
    }

    [Server]
    private void HandlePlayerCaught(GameObject playerObject)
    {
        NetworkIdentity playerIdentity = playerObject.GetComponent<NetworkIdentity>();
        if (playerIdentity == null)
        {
            // Попробуем найти в родителе
            playerIdentity = playerObject.GetComponentInParent<NetworkIdentity>();
        }

        if (playerIdentity != null && playerIdentity.connectionToClient != null)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[EnemyAI] {gameObject.name} caught player {playerObject.name}, teleporting to start position...");
            }

            TeleportPlayerToStartPosition(playerIdentity);
        }
    }

    [Server]
    private void TeleportPlayerToStartPosition(NetworkIdentity playerIdentity)
    {
        // Получаем позицию спавна
        Vector3 startPosition = GetPlayerSpawnPosition();

        // Отключаем CharacterController перед телепортацией (он блокирует изменение позиции)
        CharacterController characterController = playerIdentity.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Перемещаем игрока на точку NetworkStartPosition
        playerIdentity.transform.position = startPosition;
        playerIdentity.transform.rotation = Quaternion.identity;

        // Включаем CharacterController обратно
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Уведомляем клиента о перемещении и передаём позицию
        RpcOnPlayerTeleported(playerIdentity.connectionToClient, startPosition);
    }

    private Vector3 GetPlayerSpawnPosition()
    {
        // Используем зарегистрированные точки спавна из NetworkManager
        if (NetworkManager.startPositions.Count > 0)
        {
            Transform startPos = NetworkManager.startPositions[Random.Range(0, NetworkManager.startPositions.Count)];
            if (startPos != null)
            {
                return startPos.position;
            }
        }

        // Fallback - начальная позиция
        Debug.LogWarning("[EnemyAI] No NetworkStartPosition found!");
        return new Vector3(0, 1, 0);
    }

    [TargetRpc]
    private void RpcOnPlayerTeleported(NetworkConnectionToClient target, Vector3 position)
    {
        // На клиенте тоже нужно телепортировать с отключением CharacterController
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            // Ищем CharacterController у локального игрока
            NetworkIdentity localPlayer = NetworkClient.localPlayer;
            if (localPlayer != null)
            {
                characterController = localPlayer.GetComponent<CharacterController>();
            }
        }

        if (characterController != null)
        {
            characterController.enabled = false;
            characterController.transform.position = position;
            characterController.enabled = true;
        }

        if (_showDebugInfo)
        {
            Debug.Log($"[EnemyAI] Player teleported to start position: {position}");
        }
    }

}

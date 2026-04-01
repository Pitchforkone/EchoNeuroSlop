using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Photon.Pun;

/// <summary>
/// Враг AI на сервере с патрулированием по точкам.
/// AI логика выполняется только на MasterClient, позиция синхронизируется через PhotonTransformView.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PhotonView))]
public class EnemyAI : MonoBehaviourPun, IPunObservable
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

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;

    [Header("Kill Settings")]
    [SerializeField] private string _playerTag = "Player";

    [Header("Spawn Points")]
    [Tooltip("Точки спавна для телепортации пойманных игроков")]
    [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();

    private NavMeshAgent _agent;
    private AudioSource _audioSource;
    
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

    private int _currentTargetIndex = -1;

    /// <summary>
    /// Checks if this client is the MasterClient (server authority).
    /// </summary>
    public bool IsMasterClient => PhotonNetwork.IsMasterClient;

    public float CurrentSpeed => _agent != null ? _agent.speed : 0f;
    public bool IsChasing => _currentState == EnemyState.Chasing;

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
    }

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            if (_patrolPoints.Count == 0)
            {
                Debug.LogWarning($"[EnemyAI] No patrol points assigned to {gameObject.name}!", this);
                return;
            }
            SelectNextPatrolPoint();
        }
        else
        {
            // На не-мастер клиентах отключаем NavMeshAgent
            _agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;

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

    private void StartWaiting()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        _currentState = EnemyState.Waiting;
        _agent.isStopped = true;

        float waitTime = _currentTarget != null ? _currentTarget.WaitTime : _defaultWaitTime;
        _waitTimer = waitTime;

        if (_showDebugInfo)
        {
            Debug.Log($"[EnemyAI] {gameObject.name} reached point, waiting {waitTime}s");
        }
    }

    private void SelectNextPatrolPoint()
    {
        if (!PhotonNetwork.IsMasterClient) return;

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

    private void MoveToPatrolPoint(PatrolPoint target)
    {
        if (!PhotonNetwork.IsMasterClient) return;
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

    public void SetPursuitTarget(Vector3 position, float duration, int priority)
    {
        if (!PhotonNetwork.IsMasterClient) return;

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

    public void SetPursuitTransform(Transform target, float duration, int priority)
    {
        if (!PhotonNetwork.IsMasterClient) return;
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

    public void RefreshPursuitTarget(Transform target, float duration, int priority)
    {
        if (!PhotonNetwork.IsMasterClient) return;

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

        photonView.RPC(nameof(RpcPlayChaseSound), RpcTarget.All);

        if (_showDebugInfo)
        {
            string targetName = target != null ? target.name : "position";
            Debug.Log($"[EnemyAI] {gameObject.name} started chasing {targetName} for {duration}s (priority: {priority})");
        }
    }

    [PunRPC]
    private void RpcPlayChaseSound()
    {
        if (_chaseStartClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_chaseStartClip, _chaseStartVolume);
        }
    }

    private void EndChase()
    {
        _chaseTargetTransform = null;
        _currentChasePriority = 0;

        if (_showDebugInfo)
        {
            Debug.Log($"[EnemyAI] {gameObject.name} ended chase, resuming patrol");
        }

        SelectNextPatrolPoint();
    }

    public void CancelChase()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (_currentState == EnemyState.Chasing)
        {
            EndChase();
        }
    }

    public void AddPatrolPoint(PatrolPoint point)
    {
        if (point != null && !_patrolPoints.Contains(point))
        {
            _patrolPoints.Add(point);
        }
    }

    public void RemovePatrolPoint(PatrolPoint point)
    {
        _patrolPoints.Remove(point);
    }

    public void StopPatrol()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        _currentState = EnemyState.Idle;
        _agent.isStopped = true;
    }

    public void ResumePatrol()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (_currentState == EnemyState.Idle)
        {
            SelectNextPatrolPoint();
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (hit.gameObject.CompareTag(_playerTag))
            HandlePlayerCaught(hit.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (collision.gameObject.CompareTag(_playerTag))
            HandlePlayerCaught(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (other.CompareTag(_playerTag))
            HandlePlayerCaught(other.gameObject);
    }

    private void HandlePlayerCaught(GameObject playerObject)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        PhotonView playerPV = playerObject.GetComponent<PhotonView>();
        if (playerPV == null)
            playerPV = playerObject.GetComponentInParent<PhotonView>();

        if (playerPV != null)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[EnemyAI] {gameObject.name} caught player {playerObject.name}, teleporting to start position...");
            }

            Vector3 startPosition = GetPlayerSpawnPosition();
            playerPV.RPC(nameof(RpcOnPlayerTeleported), playerPV.Owner, startPosition);
        }
    }

    private Vector3 GetPlayerSpawnPosition()
    {
        // Use local spawn points or fallback to CustomNetworkManager
        if (_spawnPoints.Count > 0)
        {
            Transform startPos = _spawnPoints[Random.Range(0, _spawnPoints.Count)];
            if (startPos != null)
                return startPos.position;
        }

        if (CustomNetworkManager.Instance != null)
        {
            return CustomNetworkManager.Instance.GetRandomSpawnPosition();
        }

        Debug.LogWarning("[EnemyAI] No spawn position found!");
        return new Vector3(0, 1, 0);
    }

    [PunRPC]
    private void RpcOnPlayerTeleported(Vector3 position)
    {
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            // Find local player's CharacterController
            foreach (var pv in FindObjectsOfType<PhotonView>())
            {
                if (pv.IsMine)
                {
                    characterController = pv.GetComponent<CharacterController>();
                    if (characterController != null) break;
                }
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

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext((int)_currentState);
            stream.SendNext(_currentTargetIndex);
        }
        else
        {
            _currentState = (EnemyState)(int)stream.ReceiveNext();
            _currentTargetIndex = (int)stream.ReceiveNext();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_patrolPoints == null || _patrolPoints.Count == 0) return;

        Gizmos.color = Color.green;
        foreach (var point in _patrolPoints)
        {
            if (point != null)
            {
                Gizmos.DrawLine(transform.position, point.transform.position);
            }
        }

        if (_currentTarget != null && _currentState != EnemyState.Chasing)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
            Gizmos.DrawWireSphere(_currentTarget.transform.position, 0.3f);
        }

        if (_currentState == EnemyState.Chasing)
        {
            Gizmos.color = Color.red;
            Vector3 targetPos = _chaseTargetTransform != null ? _chaseTargetTransform.position : _chaseTargetPosition;
            Gizmos.DrawLine(transform.position, targetPos);
            Gizmos.DrawWireSphere(targetPos, _chaseReachDistance);
        }
    }
}

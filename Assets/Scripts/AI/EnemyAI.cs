using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Mirror;

/// <summary>
/// ������� �� ������ � �������������� �� ����.
/// ���� ����� � ��������� ����� (����� ����������), ���������������, ����� ����������.
/// ������������ ������������� �����, ������������ �����������.
/// ������ AI ����������� ������ �� �������, ������� ���������������� ����� NetworkTransform.
/// </summary>
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
    [Tooltip("������ ����� ��������������")]
    [SerializeField] private List<PatrolPoint> _patrolPoints = new List<PatrolPoint>();

    [Tooltip("����� �������� �� ���������, ���� � ����� �� �������")]
    [SerializeField] private float _defaultWaitTime = 2f;

    [Tooltip("�������� ������������ ��� ��������������")]
    [SerializeField] private float _patrolSpeed = 3.5f;

    [Header("Chase Settings")]
    [Tooltip("�������� ������������ ��� �������������")]
    [SerializeField] private float _chaseSpeed = 5f;

    [Tooltip("���������, �� ������� ���� �������, ��� ������ ���� �������������")]
    [SerializeField] private float _chaseReachDistance = 1.5f;

    [Tooltip("�������� ���������� ���� � ���������� ����")]
    [SerializeField] private float _pathUpdateInterval = 0.2f;

    [Header("Audio")]
    [Tooltip("Звук при начале преследования игрока")]
    [SerializeField] private AudioClip _chaseStartClip;

    [Tooltip("Громкость звука преследования")]
    [SerializeField] [Range(0f, 1f)] private float _chaseStartVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;

    private NavMeshAgent _agent;
    private AudioSource _audioSource;
    
    [SyncVar]
    private EnemyState _currentState = EnemyState.Idle;
    
    private PatrolPoint _currentTarget;
    private PatrolPoint _previousTarget;
    private float _waitTimer;

    // �������������
    private Vector3 _chaseTargetPosition;
    private Transform _chaseTargetTransform;
    private float _chaseDuration;
    private float _chaseTimer;
    private int _currentChasePriority;
    private float _pathUpdateTimer;

    // �������������� ������ ������� ���� ��� �������� (�����������, ��� �������)
    [SyncVar]
    private int _currentTargetIndex = -1;

    /// <summary>
    /// ������� �������� ����� (��� �������� �������).
    /// </summary>
    public float CurrentSpeed => _agent != null ? _agent.speed : 0f;

    /// <summary>
    /// ���� ������ ���������� ����.
    /// </summary>
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

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        if (_patrolPoints.Count == 0)
        {
            Debug.LogWarning($"[EnemyAI] No patrol points assigned to {gameObject.name}!", this);
            return;
        }

        // �������� �������������� � ��������� �����
        SelectNextPatrolPoint();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // �� �������� ��������� NavMeshAgent, �.�. ������� ���������������� ����� NetworkTransform
        if (!isServer)
        {
            _agent.enabled = false;
        }
    }

    private void Update()
    {
        // ������ AI ����������� ������ �� �������
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
                // ������ �� ������
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

        // ���������, �������� �� �����
        float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
        
        if (distanceToTarget <= _currentTarget.ReachRadius || 
            (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance))
        {
            // �������� ����� - �������� ��������
            StartWaiting();
        }
    }

    private void UpdateWaiting()
    {
        _waitTimer -= Time.deltaTime;

        if (_waitTimer <= 0f)
        {
            // ����� �������� ������� - ��� � ��������� �����
            SelectNextPatrolPoint();
        }
    }

    private void UpdateChasing()
    {
        _chaseTimer -= Time.deltaTime;

        // ����� ������������� �������
        if (_chaseTimer <= 0f)
        {
            EndChase();
            return;
        }

        // ��������� ���� � ���������� ����
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

        // ���������, �������� �� ����
        float distanceToTarget = Vector3.Distance(transform.position, _chaseTargetPosition);
        if (distanceToTarget <= _chaseReachDistance)
        {
            if (_showDebugInfo)
            {
                Debug.Log($"[EnemyAI] {gameObject.name} reached chase target");
            }

            // ���� ���������� Transform - ����������, ����� �����������
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

    /// <summary>
    /// ������� ��������� ����� ��������������, �������� ����������.
    /// </summary>
    private PatrolPoint FindNearestPatrolPoint()
    {
        PatrolPoint nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var point in _patrolPoints)
        {
            if (point == null) continue;

            // ���������� ���������� ����� (����� ���� ����� ������)
            if (point == _previousTarget && _patrolPoints.Count > 1) continue;

            // ���������� ������� �����
            if (point == _currentTarget) continue;

            float distance = Vector3.Distance(transform.position, point.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = point;
            }
        }

        // ���� �� ����� (��������, ������ ���������� �����), ���������� �
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

    /// <summary>
    /// ������������� ���� ������������� (��������� �������).
    /// ���������� �����������.
    /// </summary>
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
}

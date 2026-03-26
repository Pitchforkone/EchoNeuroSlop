using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Базовый ИИ врага с патрулированием по точкам.
/// Враг ходит к ближайшей точке (кроме предыдущей), останавливается, затем продолжает.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Idle,
        Walking,
        Waiting
    }

    [Header("Patrol Settings")]
    [Tooltip("Список точек патрулирования")]
    [SerializeField] private List<PatrolPoint> _patrolPoints = new List<PatrolPoint>();

    [Tooltip("Время ожидания по умолчанию, если у точки не задано")]
    [SerializeField] private float _defaultWaitTime = 2f;

    [Tooltip("Скорость передвижения")]
    [SerializeField] private float _moveSpeed = 3.5f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;

    private NavMeshAgent _agent;
    private EnemyState _currentState = EnemyState.Idle;
    private PatrolPoint _currentTarget;
    private PatrolPoint _previousTarget;
    private float _waitTimer;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = _moveSpeed;
    }

    private void Start()
    {
        if (_patrolPoints.Count == 0)
        {
            Debug.LogWarning($"[EnemyAI] No patrol points assigned to {gameObject.name}!", this);
            return;
        }

        // Начинаем патрулирование с ближайшей точки
        SelectNextPatrolPoint();
    }

    private void Update()
    {
        switch (_currentState)
        {
            case EnemyState.Walking:
                UpdateWalking();
                break;

            case EnemyState.Waiting:
                UpdateWaiting();
                break;

            case EnemyState.Idle:
                // Ничего не делаем
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

        // Проверяем, достигли ли точки
        float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.transform.position);
        
        if (distanceToTarget <= _currentTarget.ReachRadius || 
            (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance))
        {
            // Достигли точки - начинаем ожидание
            StartWaiting();
        }
    }

    private void UpdateWaiting()
    {
        _waitTimer -= Time.deltaTime;

        if (_waitTimer <= 0f)
        {
            // Время ожидания истекло - идём к следующей точке
            SelectNextPatrolPoint();
        }
    }

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

        MoveToTarget(_currentTarget);
    }

    /// <summary>
    /// Находит ближайшую точку патрулирования, исключая предыдущую.
    /// </summary>
    private PatrolPoint FindNearestPatrolPoint()
    {
        PatrolPoint nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var point in _patrolPoints)
        {
            if (point == null) continue;

            // Пропускаем предыдущую точку (если точек больше одной)
            if (point == _previousTarget && _patrolPoints.Count > 1) continue;

            // Пропускаем текущую точку
            if (point == _currentTarget) continue;

            float distance = Vector3.Distance(transform.position, point.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = point;
            }
        }

        // Если не нашли (осталась только предыдущая точка), возвращаем её
        if (nearest == null && _previousTarget != null)
        {
            nearest = _previousTarget;
        }

        return nearest;
    }

    private void MoveToTarget(PatrolPoint target)
    {
        if (target == null) return;

        _agent.isStopped = false;
        _agent.SetDestination(target.transform.position);
        _currentState = EnemyState.Walking;

        if (_showDebugInfo)
        {
            Debug.Log($"[EnemyAI] {gameObject.name} moving to {target.name}");
        }
    }

    /// <summary>
    /// Добавляет точку патрулирования в список.
    /// </summary>
    public void AddPatrolPoint(PatrolPoint point)
    {
        if (point != null && !_patrolPoints.Contains(point))
        {
            _patrolPoints.Add(point);
        }
    }

    /// <summary>
    /// Удаляет точку патрулирования из списка.
    /// </summary>
    public void RemovePatrolPoint(PatrolPoint point)
    {
        _patrolPoints.Remove(point);
    }

    /// <summary>
    /// Останавливает патрулирование.
    /// </summary>
    public void StopPatrol()
    {
        _currentState = EnemyState.Idle;
        _agent.isStopped = true;
    }

    /// <summary>
    /// Возобновляет патрулирование.
    /// </summary>
    public void ResumePatrol()
    {
        if (_currentState == EnemyState.Idle)
        {
            SelectNextPatrolPoint();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_patrolPoints == null || _patrolPoints.Count == 0) return;

        // Рисуем линии от врага к точкам
        Gizmos.color = Color.green;
        foreach (var point in _patrolPoints)
        {
            if (point != null)
            {
                Gizmos.DrawLine(transform.position, point.transform.position);
            }
        }

        // Рисуем текущую цель
        if (_currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
            Gizmos.DrawWireSphere(_currentTarget.transform.position, 0.3f);
        }
    }
}

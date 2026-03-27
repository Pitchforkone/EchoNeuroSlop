using UnityEngine;

/// <summary>
/// Базовый класс для детекторов врага.
/// Дочерние объекты с этим компонентом обнаруживают цели и сообщают EnemyAI.
/// Включает kinematic Rigidbody для работы триггер-триггер коллизий.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public abstract class EnemyDetectorBase : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Время преследования цели после обнаружения")]
    [SerializeField] protected float _pursuitDuration = 5f;

    [Tooltip("Приоритет этого детектора (выше = важнее)")]
    [SerializeField] protected int _priority = 0;

    [Header("Debug")]
    [SerializeField] protected bool _showDebugInfo = true;

    protected EnemyAI _enemyAI;
    protected Collider _collider;
    protected Rigidbody _rigidbody;

    /// <summary>
    /// Приоритет детектора. Более высокий приоритет перебивает текущее преследование.
    /// </summary>
    public int Priority => _priority;

    /// <summary>
    /// Время преследования для этого типа детекции.
    /// </summary>
    public float PursuitDuration => _pursuitDuration;

    protected virtual void Awake()
    {
        _collider = GetComponent<Collider>();
        
        // Убеждаемся, что коллайдер является триггером
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }

        // Setup Rigidbody as kinematic (required for trigger-trigger collision detection)
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;

        // Ищем EnemyAI в родительских объектах
        _enemyAI = GetComponentInParent<EnemyAI>();
        
        if (_enemyAI == null)
        {
            Debug.LogError($"[{GetType().Name}] No EnemyAI found in parent hierarchy of {gameObject.name}!", this);
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (_enemyAI == null) return;
        
        // Проверка только на сервере
        if (!_enemyAI.isServer) return;

        if (_showDebugInfo)
        {
            Debug.Log($"[{GetType().Name}] OnTriggerEnter with {other.name}");
        }

        ProcessCollision(other);
    }

    protected virtual void OnTriggerStay(Collider other)
    {
        // Можно переопределить в наследниках для постоянного отслеживания
    }

    /// <summary>
    /// Обрабатывает коллизию. Переопределяется в наследниках.
    /// </summary>
    protected abstract void ProcessCollision(Collider other);

    /// <summary>
    /// Уведомляет EnemyAI о обнаруженной цели.
    /// </summary>
    protected void NotifyTargetDetected(Vector3 targetPosition, float duration, int priority)
    {
        if (_enemyAI == null) return;

        if (_showDebugInfo)
        {
            Debug.Log($"[{GetType().Name}] {_enemyAI.gameObject.name} detected target at {targetPosition}, calling SetPursuitTarget");
        }

        _enemyAI.SetPursuitTarget(targetPosition, duration, priority);
    }

    /// <summary>
    /// Уведомляет EnemyAI о обнаруженном трансформе для постоянного преследования.
    /// </summary>
    protected void NotifyTransformDetected(Transform target, float duration, int priority)
    {
        if (_enemyAI == null) return;

        if (_showDebugInfo)
        {
            Debug.Log($"[{GetType().Name}] {_enemyAI.gameObject.name} detected transform {target.name}, calling SetPursuitTransform");
        }

        _enemyAI.SetPursuitTransform(target, duration, priority);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        // Визуализация коллайдера
        var col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);

        if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius * transform.lossyScale.x);
        }
        else if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}

using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Детектор эхо-волн с накоплением.
/// Суммирует количество эхо в области, имеет скорость убывания шкалы.
/// При превышении порога начинает преследование последней позиции эхо.
/// При достижении точки эхо враг останавливается на время, затем возвращается к патрулированию.
/// </summary>
public class EnemyEchoAccumulatorDetector : EnemyDetectorBase
{
    [Header("Echo Accumulation Settings")]
    [Tooltip("Типы эхо, которые игнорируются детектором")]
    [SerializeField] private List<EchoType> _ignoredEchoTypes = new List<EchoType>();

    [Tooltip("Сколько добавляется к шкале за каждое обнаруженное эхо")]
    [SerializeField] private float _echoValue = 1f;

    [Tooltip("Скорость убывания шкалы (единиц в секунду)")]
    [SerializeField] private float _decayRate = 0.5f;

    [Tooltip("Пороговое значение для начала преследования")]
    [SerializeField] private float _activationThreshold = 3f;

    [Header("Arrival Settings")]
    [Tooltip("Расстояние, на котором считается что враг достиг точки эхо")]
    [SerializeField] private float _arrivalDistance = 1.5f;

    [Tooltip("Время ожидания после достижения точки эхо (секунды)")]
    [SerializeField] private float _waitTimeOnArrival = 3f;

    // Текущее значение шкалы накопления
    [SerializeField] private float _currentAccumulation = 0f;

    // Позиция последнего обнаруженного эхо
    private Vector3 _lastEchoPosition;

    // Флаг активного преследования
    private bool _isPursuing = false;

    // Флаг ожидания на месте
    private bool _isWaiting = false;

    // Таймер ожидания
    private float _waitTimer = 0f;

    // HashSet для быстрой проверки игнорируемых типов
    private HashSet<EchoType> _ignoredTypesSet;

    /// <summary>
    /// Текущее значение накопления (для отладки).
    /// </summary>
    public float CurrentAccumulation => _currentAccumulation;

    /// <summary>
    /// Идёт ли сейчас преследование.
    /// </summary>
    public bool IsPursuing => _isPursuing;

    /// <summary>
    /// Ожидает ли враг на месте.
    /// </summary>
    public bool IsWaiting => _isWaiting;

    protected override void Awake()
    {
        base.Awake();

        // Создаём HashSet для быстрой проверки
        _ignoredTypesSet = new HashSet<EchoType>(_ignoredEchoTypes);
    }

    private void Update()
    {
        if (_enemyAI == null) return;
        if (!_enemyAI.isServer) return;

        // Обработка состояния ожидания
        if (_isWaiting)
        {
            _waitTimer -= Time.deltaTime;

            if (_waitTimer <= 0f)
            {
                // Время ожидания истекло - возвращаемся к патрулированию
                _isWaiting = false;
                _isPursuing = false;
                _currentAccumulation = 0f;
                _enemyAI.ResumePatrol();
            }
            return;
        }

        // Уменьшаем шкалу со временем
        if (_currentAccumulation > 0)
        {
            _currentAccumulation -= _decayRate * Time.deltaTime;
            _currentAccumulation = Mathf.Max(_currentAccumulation, 0);
        }

        // Проверяем состояние преследования
        if (_isPursuing)
        {
            // Проверяем, достиг ли враг точки эхо
            float distanceToTarget = Vector3.Distance(_enemyAI.transform.position, _lastEchoPosition);
            if (distanceToTarget <= _arrivalDistance)
            {
                // Враг достиг точки - останавливаем и начинаем ожидание
                StartWaiting();
                return;
            }

            // Если шкала упала до минимума - прекращаем преследование
            if (_currentAccumulation <= 0)
            {
                _isPursuing = false;
                _enemyAI.ResumePatrol();
            }
            else
            {
                // Продолжаем уведомлять о цели пока идёт преследование
                NotifyTargetDetected(_lastEchoPosition, _pursuitDuration, _priority);
            }
        }
    }

    /// <summary>
    /// Начинает состояние ожидания на месте.
    /// </summary>
    private void StartWaiting()
    {
        _isWaiting = true;
        _waitTimer = _waitTimeOnArrival;
        _enemyAI.StopPatrol();
    }

    protected override void ProcessCollision(Collider other)
    {
        // Если ожидаем - не обрабатываем новые эхо
        if (_isWaiting) return;

        // Проверяем, что это EchoCollider
        var echoCollider = other.GetComponent<EchoCollider>();
        if (echoCollider == null) return;

        // Проверяем, не игнорируется ли этот тип эхо
        if (IsEchoTypeIgnored(echoCollider.EchoType)) return;

        // Добавляем значение к шкале
        _currentAccumulation += _echoValue;

        // Сохраняем позицию последнего эхо
        _lastEchoPosition = other.transform.position;

        // Проверяем, достигнут ли порог
        if (_currentAccumulation >= _activationThreshold && !_isPursuing)
        {
            _isPursuing = true;
            NotifyTargetDetected(_lastEchoPosition, _pursuitDuration, _priority);
        }
        else if (_isPursuing)
        {
            // Обновляем цель, если уже преследуем
            NotifyTargetDetected(_lastEchoPosition, _pursuitDuration, _priority);
        }
    }

    /// <summary>
    /// Проверяет, игнорируется ли данный тип эхо.
    /// </summary>
    private bool IsEchoTypeIgnored(EchoType echoType)
    {
        return _ignoredTypesSet.Contains(echoType);
    }

    /// <summary>
    /// Сбросить накопление (для внешнего использования).
    /// </summary>
    public void ResetAccumulation()
    {
        _currentAccumulation = 0;
        _isPursuing = false;
        _isWaiting = false;
        _waitTimer = 0f;
    }

    /// <summary>
    /// Добавить тип эхо в список игнорируемых.
    /// </summary>
    public void AddIgnoredEchoType(EchoType echoType)
    {
        if (!_ignoredTypesSet.Contains(echoType))
        {
            _ignoredTypesSet.Add(echoType);
            _ignoredEchoTypes.Add(echoType);
        }
    }

    /// <summary>
    /// Удалить тип эхо из списка игнорируемых.
    /// </summary>
    public void RemoveIgnoredEchoType(EchoType echoType)
    {
        if (_ignoredTypesSet.Contains(echoType))
        {
            _ignoredTypesSet.Remove(echoType);
            _ignoredEchoTypes.Remove(echoType);
        }
    }
}

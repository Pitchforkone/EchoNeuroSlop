using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Детектор эхо-волн с накоплением.
/// Суммирует количество эхо в области, имеет скорость убывания шкалы.
/// При превышении порога начинает преследование последней позиции эхо.
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

    // Текущее значение шкалы накопления
    [SerializeField] private float _currentAccumulation = 0f;

    // Позиция последнего обнаруженного эхо
    private Vector3 _lastEchoPosition;

    // Флаг активного преследования
    private bool _isPursuing = false;

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

        // Уменьшаем шкалу со временем
        if (_currentAccumulation > 0)
        {
            _currentAccumulation -= _decayRate * Time.deltaTime;
            _currentAccumulation = Mathf.Max(_currentAccumulation, 0);
        }

        // Проверяем состояние преследования
        if (_isPursuing)
        {
            // Если шкала упала до минимума - прекращаем преследование
            if (_currentAccumulation <= 0)
            {
                _isPursuing = false;
            }
            else
            {
                // Продолжаем уведомлять о цели пока идёт преследование
                NotifyTargetDetected(_lastEchoPosition, _pursuitDuration, _priority);
            }
        }
    }

    protected override void ProcessCollision(Collider other)
    {
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

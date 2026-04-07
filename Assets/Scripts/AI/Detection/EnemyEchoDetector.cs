using UnityEngine;

/// <summary>
/// Детектор эхо-волн. Реагирует на EchoCollider с определённым EchoType.
/// Размещается как дочерний объект EnemyAI.
/// </summary>
public class EnemyEchoDetector : EnemyDetectorBase
{
    [Header("Echo Detection")]
    [Tooltip("На какие типы эхо реагировать. Если None - реагирует на все.")]
    [SerializeField] private EchoType _detectableEchoType = EchoType.NonTrigger;

    [Tooltip("Реагировать на все типы эхо")]
    [SerializeField] private bool _detectAllTypes = true;

    [Tooltip("Минимальная интенсивность эхо для обнаружения")]
    [SerializeField] private float _minIntensity = 0.5f;

    protected override void ProcessCollision(Collider other)
    {
        // Проверяем, что это EchoCollider
        var echoCollider = other.GetComponent<EchoCollider>();
        if (echoCollider == null) return;

        // Проверяем тип эхо
        if (!IsEchoTypeDetectable(echoCollider.EchoType)) return;

        // Уведомляем о позиции эхо (статичная точка)
        NotifyTargetDetected(other.transform.position, _pursuitDuration, _priority);
    }

    /// <summary>
    /// Проверяет, нужно ли реагировать на данный тип эхо.
    /// </summary>
    private bool IsEchoTypeDetectable(EchoType echoType)
    {
        // Если реагируем на все типы
        if (_detectAllTypes)
        {
            return true;
        }

        // Проверяем совпадение типа
        return _detectableEchoType == echoType;
    }
}

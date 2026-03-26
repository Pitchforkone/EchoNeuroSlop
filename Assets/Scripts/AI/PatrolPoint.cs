using UnityEngine;

/// <summary>
/// Точка патрулирования для ИИ врага.
/// Размещается на сцене для обозначения мест остановки.
/// </summary>
public class PatrolPoint : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Время ожидания на этой точке (в секундах)")]
    [SerializeField] private float _waitTime = 2f;

    [Tooltip("Радиус достижения точки")]
    [SerializeField] private float _reachRadius = 0.5f;

    /// <summary>
    /// Время ожидания на этой точке.
    /// </summary>
    public float WaitTime => _waitTime;

    /// <summary>
    /// Радиус, при котором точка считается достигнутой.
    /// </summary>
    public float ReachRadius => _reachRadius;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _reachRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _reachRadius);
        
        // Показываем время ожидания
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"Wait: {_waitTime}s");
#endif
    }
}

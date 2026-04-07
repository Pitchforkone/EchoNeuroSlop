using UnityEngine;

/// <summary>
/// Детектор игрока. Реагирует на прямой контакт с PlayerController.
/// Размещается как дочерний объект EnemyAI.
/// </summary>
public class EnemyPlayerDetector : EnemyDetectorBase
{
    [Header("Player Detection")]
    [Tooltip("Отслеживать позицию игрока постоянно (true) или только точку обнаружения (false)")]
    [SerializeField] private bool _trackPlayerPosition = true;

    [Tooltip("Слой игрока для дополнительной фильтрации")]
    [SerializeField] private LayerMask _playerLayer = -1;

    protected override void Awake()
    {
        base.Awake();

        // Для детектора игрока приоритет обычно выше
        if (_priority == 0)
        {
            _priority = 10;
        }

        // Время преследования по умолчанию
        if (_pursuitDuration <= 0f)
        {
            _pursuitDuration = 3f;
        }
    }

    protected override void ProcessCollision(Collider other)
    {
        // Проверяем слой
        if (_playerLayer != -1 && ((1 << other.gameObject.layer) & _playerLayer) == 0)
        {
            return;
        }

        // Проверяем, что это PlayerController
        var player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            // Также проверяем в родителях
            player = other.GetComponentInParent<PlayerController>();
        }

        if (player == null) return;

        if (_trackPlayerPosition)
        {
            // Передаём Transform для постоянного отслеживания
            NotifyTransformDetected(player.transform, _pursuitDuration, _priority);
        }
        else
        {
            // Передаём только текущую позицию
            NotifyTargetDetected(player.transform.position, _pursuitDuration, _priority);
        }
    }

    protected override void OnTriggerStay(Collider other)
    {
        // Обновляем преследование пока игрок в зоне детекции
        if (_enemyAI == null || !_enemyAI.isServer) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController>();
        }

        if (player != null && _trackPlayerPosition)
        {
            // Обновляем цель, продлевая время преследования
            _enemyAI.RefreshPursuitTarget(player.transform, _pursuitDuration, _priority);
        }
    }
}

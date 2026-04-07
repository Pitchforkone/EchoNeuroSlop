using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyRestrictedZone : MonoBehaviour
{
    private Collider _collider;
    private Rigidbody _rigidbody;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }

        // Rigidbody нужен для срабатывания триггеров
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        EnemyAI enemyAI = GetEnemyAI(other);

        if (enemyAI == null) return;

        if (!enemyAI.isServer) return;


        enemyAI.CancelChase();
    }

    private void OnTriggerStay(Collider other)
    {
        EnemyAI enemyAI = GetEnemyAI(other);

        if (enemyAI == null) return;

        if (!enemyAI.isServer) return;

        if (enemyAI.IsChasing)
        {
            enemyAI.CancelChase();
        }
    }

    private EnemyAI GetEnemyAI(Collider other)
    {
        if (other.TryGetComponent<EnemyAI>(out var enemyAI))
            return enemyAI;
        else return null;
    }
}

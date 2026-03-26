using UnityEngine;

/// <summary>
/// ��������� �������, ������� ������������ ������ ���-�������.
/// </summary>
public class EchoGrenade : MonoBehaviour
{
    private EchoPreset _echoPreset;
    private float _blinkInterval;
    private float _lifetime;

    private float _blinkTimer;
    private float _lifeTimer;
    private bool _initialized;


    /// <summary>
    /// Инициализирует гранату.
    /// </summary>
    public void Initialize(EchoPreset echoPreset, float blinkInterval, float lifetime)
    {
        _echoPreset = echoPreset;
        _blinkInterval = blinkInterval;
        _lifetime = lifetime;

        _blinkTimer = 0f;
        _lifeTimer = lifetime;
        _initialized = true;

        // Первая вспышка сразу
        SpawnEcho();
    }

    private void Update()
    {
        if (!_initialized) return;

        // Обратный отсчёт времени жизни
        _lifeTimer -= Time.deltaTime;
        if (_lifeTimer <= 0f)
        {
            // Финальная мощная вспышка перед уничтожением
            SpawnFinalEcho();
            Destroy(gameObject);
            return;
        }

        // Таймер вспышек
        _blinkTimer -= Time.deltaTime;
        if (_blinkTimer <= 0f)
        {
            SpawnEcho();
            _blinkTimer = _blinkInterval;
        }
    }

    /// <summary>
    /// Обработка столкновения с объектами.
    /// При касании объектов (не Ground) создаётся вспышка.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (!_initialized) return;

        // Если столкнулись не с землёй — создаём вспышк
        SpawnEcho();
    }

    /// <summary>
    /// Создаёт эхо-вспышку.
    /// </summary>
    private void SpawnEcho()
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        EchoManager.Instance.SpawnEcho(transform.position, _echoPreset);
    }

    /// <summary>
    /// Создаёт финальную усиленную вспышку.
    /// </summary>
    private void SpawnFinalEcho()
    {
        if (_echoPreset == null || EchoManager.Instance == null) return;

        // ��������� ��������� �������
        EchoManager.Instance.SpawnEcho(
            transform.position,
            _echoPreset.Speed,
            _echoPreset.MaxRadius * 1.5f,
            _echoPreset.Intensity * 2f,
            _echoPreset.Color,
            _echoPreset.Lifetime * 1.5f
        );
    }
}

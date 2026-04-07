using UnityEngine;
/// <summary>
/// Компонент для врагов, который создаёт эхо при ходьбе.
/// Метод EmitFootstepEcho() вызывается через Animation Events.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class EnemyWalkEcho : MonoBehaviour
{
    [Header("Echo Settings")]
    [Tooltip("Пресет эхо для шагов врага")]
    [SerializeField] private EchoPreset _footstepPreset;

    [Header("Foot Transforms")]
    [Tooltip("Трансформ левой ноги (опционально, если не задан - используется позиция объекта)")]
    [SerializeField] private Transform _leftFoot;

    [Tooltip("Трансформ правой ноги (опционально, если не задан - используется позиция объекта)")]
    [SerializeField] private Transform _rightFoot;

    [Header("Ground Offset")]
    [Tooltip("Смещение по Y для позиции эхо (обычно 0 для эхо на уровне земли)")]
    [SerializeField] private float _groundOffset = 0.05f;

    [Header("Audio Settings")]
    [Tooltip("Список звуков шагов (воспроизводится случайный)")]
    [SerializeField] private AudioClip[] _footstepSounds;

    [Tooltip("Громкость звука шагов")]
    [Range(0f, 1f)]
    [SerializeField] private float _footstepVolume = 0.5f;

    [Tooltip("Случайное отклонение высоты звука")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _pitchVariation = 0.1f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.spatialBlend = 1f; // 3D звук
        _audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Вызывается через Animation Event для левой ноги.
    /// </summary>
    public void EmitLeftFootstepEcho()
    {
        EmitFootstepEcho(_leftFoot);
    }

    /// <summary>
    /// Вызывается через Animation Event для правой ноги.
    /// </summary>
    public void EmitRightFootstepEcho()
    {
        EmitFootstepEcho(_rightFoot);
    }

    /// <summary>
    /// Универсальный метод для вызова через Animation Event.
    /// Используется когда не важно какая нога (или нет отдельных трансформов).
    /// </summary>
    public void EmitFootstepEcho()
    {
        EmitFootstepEcho(null);
    }

    private void EmitFootstepEcho(Transform footTransform)
    {
        if (_footstepPreset == null)
        {
            Debug.LogWarning($"[EnemyWalkEcho] EchoPreset не назначен на {gameObject.name}");
            return;
        }

        if (EchoManager.Instance == null)
        {
            Debug.LogWarning("[EnemyWalkEcho] EchoManager.Instance не найден");
            return;
        }

        Vector3 echoPosition;

        if (footTransform != null)
        {
            // Используем позицию ноги с небольшим смещением к земле
            echoPosition = footTransform.position;
            echoPosition.y = transform.position.y + _groundOffset;
        }
        else
        {
            // Используем позицию объекта
            echoPosition = transform.position;
            echoPosition.y += _groundOffset;
        }

        EchoManager.Instance.SpawnEcho(echoPosition, _footstepPreset);

        PlayFootstepSound();
    }

    private void PlayFootstepSound()
    {
        if (_footstepSounds == null || _footstepSounds.Length == 0)
            return;

        // Выбираем случайный звук
        AudioClip clip = _footstepSounds[Random.Range(0, _footstepSounds.Length)];

        if (clip == null)
            return;

        // Добавляем небольшую вариацию высоты звука для разнообразия
        _audioSource.pitch = 1f + Random.Range(-_pitchVariation, _pitchVariation);
        _audioSource.PlayOneShot(clip, _footstepVolume);
    }
}

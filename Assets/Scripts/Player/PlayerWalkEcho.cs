using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerWalkEcho : MonoBehaviour
{
    [Header("Foot Transforms")]
    [Tooltip("Трансформа левой ноги (опционально, если не задана - используется позиция объекта)")]
    [SerializeField] private Transform _leftFoot;

    [Tooltip("Трансформа правой ноги (опционально, если не задана - используется позиция объекта)")]
    [SerializeField] private Transform _rightFoot;

    [Header("Ground Offset")]
    [Tooltip("Смещение по Y для позиции эхо (обычно 0 или чуть от уровня земли)")]
    [SerializeField] private float _groundOffset = 0.05f;

    [Header("Audio Settings")]
    [Tooltip("Громкость звука шагов")]
    [Range(0f, 1f)]
    [SerializeField] private float _footstepVolume = 0.5f;

    [Tooltip("Вариация случайного питча звука для естественности")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _pitchVariation = 0.1f;

    [Header("Sprint Detection")]
    [Tooltip("Использовать пресет спринта при беге (требуется FPSController или PlayerController)")]
    [SerializeField] private bool _useSprintPreset = true;

    private AudioSource _audioSource;
    private static EchoLocatorConfig _config;
    private FPSController _fpsController;
    private PlayerController _playerController;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Для собственных шагов игрока используем 2D звук
        // чтобы громкость не зависела от расстояния до камеры
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;
        _audioSource.pitch = 1f;
        _audioSource.volume = 1f;

        if (_config == null)
            _config = Resources.Load<EchoLocatorConfig>("EchoLocatorConfig");

        _fpsController = GetComponent<FPSController>();
        _playerController = GetComponent<PlayerController>();
    }

    /// <summary>
    /// Получает текущий пресет эхо в зависимости от состояния игрока.
    /// </summary>
    private EchoPreset GetCurrentPreset()
    {
        if (_config == null)
            return null;

        if (_useSprintPreset && IsSprinting())
        {
            return _config.SprintFootstepPreset ?? _config.FootstepPreset;
        }

        return _config.FootstepPreset;
    }

    /// <summary>
    /// Проверяет, бежит ли игрок.
    /// </summary>
    private bool IsSprinting()
    {
        if (_fpsController != null)
        {
            return _fpsController.IsSprinting;
        }

        if (_playerController != null)
        {
            return _playerController.IsSprinting;
        }

        return false;
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
    /// Используется когда нет конкретной ноги (или для обобщённого шага).
    /// </summary>
    public void EmitFootstepEcho()
    {
        EmitFootstepEcho(null);
    }

    private void EmitFootstepEcho(Transform footTransform)
    {
        EchoPreset preset = GetCurrentPreset();

        if (preset == null)
        {
            Debug.LogWarning($"[PlayerWalkEcho] EchoPreset не загружен для {gameObject.name}. Проверьте EchoLocatorConfig в Resources.");
            return;
        }

        if (EchoManager.Instance == null)
        {
            Debug.LogWarning("[PlayerWalkEcho] EchoManager.Instance не найден");
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

        EchoManager.Instance.SpawnEcho(echoPosition, preset);

        PlayFootstepSound();
    }

    private void PlayFootstepSound()
    {
        if (_config == null || _config.FootstepSounds == null || _config.FootstepSounds.Length == 0)
            return;

        // Проверяем и восстанавливаем AudioSource если нужно
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.spatialBlend = 0f;
            _audioSource.playOnAwake = false;
            _audioSource.pitch = 1f;
            _audioSource.volume = 1f;
        }

        // Выбираем случайный звук
        AudioClip clip = _config.FootstepSounds[Random.Range(0, _config.FootstepSounds.Length)];

        if (clip == null)
            return;

        // Устанавливаем pitch с вариацией (всегда от базового значения 1)
        _audioSource.pitch = 1f + Random.Range(-_pitchVariation, _pitchVariation);
        _audioSource.PlayOneShot(clip, _footstepVolume);
    }
}

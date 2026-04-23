using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerWalkEcho : MonoBehaviour
{
    [Header("Foot Transforms")]
    [SerializeField] private Transform _leftFoot;
    [SerializeField] private Transform _rightFoot;
    private float _groundOffset = 0.05f;

    [Header("Audio Settings")]
    [UnityEngine.Range(0f, 1f)]
    [SerializeField] private float _footstepVolume = 0.5f;

    [UnityEngine.Range(0f, 0.5f)]
    [SerializeField] private float _pitchVariation = 0.1f;

    private AudioSource _audioSource;
    private static EchoLocatorConfig _config;
    private PlayerController _playerController;
    private PlayerCrouch _playerCrouch;

    private List<TypeOfStep> _stepTypes = new() { TypeOfStep.Default };
    private TypeOfStep _currentTypeOfStep = TypeOfStep.Default;

    public bool IsRunning => IsSprinting();
    public bool IsCrouching => _playerCrouch != null && _playerCrouch.IsCrouching;

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

        _playerController = GetComponent<PlayerController>();
        _playerCrouch = GetComponent<PlayerCrouch>();
    }

    /// <summary>
    /// Получает текущий пресет эхо в зависимости от состояния игрока.
    /// </summary>
    private EchoTypeStep GetCurrentPreset()
    {
        if (_config == null)
            return null;
        return _config.GetPresetForStep(_currentTypeOfStep, IsRunning, IsCrouching);
    }

    /// <summary>
    /// Проверяет, бежит ли игрок.
    /// </summary>
    private bool IsSprinting()
    {

/*        if (_playerController != null)
        {
            return _playerController.IsSprinting;
        }*/

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

    private void EmitFootstepEcho(Transform footTransform)
    {
        EchoTypeStep preset = GetCurrentPreset();

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

        EchoManager.Instance.SpawnEcho(echoPosition, preset.echoPreset);

        PlayFootstepSound(preset);
    }

    private void PlayFootstepSound(EchoTypeStep preset)
    {
        if (_config == null || preset.FootstepSounds == null || preset.FootstepSounds.Length == 0)
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
        AudioClip clip = preset.FootstepSounds[Random.Range(0, preset.FootstepSounds.Length)];

        if (clip == null)
            return;

        // Устанавливаем pitch с вариацией (всегда от базового значения 1)
        _audioSource.pitch = 1f + Random.Range(-_pitchVariation, _pitchVariation);
        _audioSource.PlayOneShot(clip, _footstepVolume);
    }
    public void SetTypeOfStep(TypeOfStep typeOfStep)
    {
        _stepTypes.Add(typeOfStep);
        _currentTypeOfStep = _stepTypes.Max();
    }
    public void RemoveTypeOfStep(TypeOfStep typeOfStep)
    {
        _stepTypes.Remove(typeOfStep);
        _currentTypeOfStep = _stepTypes.Count > 0 ? _stepTypes.Max() : TypeOfStep.Default;
    }
}

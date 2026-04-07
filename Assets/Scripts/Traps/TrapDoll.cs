using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro.EditorUtilities;

/// <summary>
/// Кукла-ловушка. При входе игрока в зону начинает кричать (издавать эхо).
/// При выходе игрока запускает таймер, по истечении которого перестаёт кричать.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(AudioSource))]
public class TrapDoll : MonoBehaviour
{
    [Header("Echo Settings")]
    [Tooltip("Пресет эхо для крика куклы")]
    [SerializeField] private EchoPreset _screamPreset;

    [Tooltip("Интервал между криками (секунды)")]
    [SerializeField] private float _screamInterval = 1.5f;

    [Header("Detection Settings")]
    [Tooltip("Радиус сферы обнаружения")]
    [SerializeField] private float _detectionRadius = 5f;

    [Header("Cooldown Settings")]
    [Tooltip("Время после выхода игрока до прекращения крика (секунды)")]
    [SerializeField] private float _cooldownDuration = 3f;

    [Header("Audio Settings")]
    [Tooltip("Аудио клип крика куклы")]
    [SerializeField] private AudioClip _screamClip;

    [Tooltip("Громкость крика")]
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;

    [Tooltip("Зацикливать аудио (true) или воспроизводить с интервалом (false)")]
    [SerializeField] private bool _loopAudio = true;

    private SphereCollider _sphereCollider;
    private AudioSource _audioSource;
    private int _playerInZone;
    private bool echoPlay = false;
    private float _cd;

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        _sphereCollider.isTrigger = true;
        _sphereCollider.radius = _detectionRadius;

        _audioSource = GetComponent<AudioSource>();
        SetupAudioSource();
    }

    /// <summary>
    /// Настраивает AudioSource.
    /// </summary>
    private void SetupAudioSource()
    {
        _audioSource.Stop(); // Останавливаем на случай если Play On Awake был включён в инспекторе
        _audioSource.clip = _screamClip;
        _audioSource.loop = _loopAudio;
        _audioSource.volume = _volume;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f; // 3D звук
    }

    private void Update()
    {
        if(_playerInZone > 0 && !echoPlay)
        {
            PlayAudioWithEcho();
            return;
        }
        if(_playerInZone == 0 && _cd > 0)
        {
            _cd -= Time.deltaTime;
            if (!echoPlay) 
                PlayAudioWithEcho();
            return;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<PlayerController>(out var _controller))
        {
            _playerInZone++;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var _controller))
        {
            _playerInZone--;
        }
    }
    public void PlayAudioWithEcho()
    {
        if (_screamClip == null) return;
        StartCoroutine(PlayAudioWithEchoCoroutine(_screamClip, _screamInterval));
    }

    private IEnumerator PlayAudioWithEchoCoroutine(AudioClip clip, float echoInterval)
    {
        if (clip == null || _audioSource == null || _screamPreset == null) yield break;
        echoPlay = true;
        // Воспроизводим аудио
        _audioSource.PlayOneShot(clip, _volume);

        float clipLength = clip.length;
        float elapsed = 0f;

        // Издаём первое эхо сразу
        if (EchoManager.Instance != null)
        {
            EchoManager.Instance.SpawnEcho(transform.position, _screamPreset);
        }

        // Издаём эхо с интервалом пока играет клип
        while (elapsed < clipLength)
        {
            yield return new WaitForSeconds(echoInterval);
            elapsed += echoInterval;

            if (elapsed < clipLength && EchoManager.Instance != null)
            {
                EchoManager.Instance.SpawnEcho(transform.position, _screamPreset);
            }
        }
        if(_playerInZone >0 )_cd = _cooldownDuration;
        echoPlay = false;
    }

    private void OnValidate()
    {
        // Синхронизируем радиус коллайдера с настройками
        if (_sphereCollider == null)
        {
            _sphereCollider = GetComponent<SphereCollider>();
        }

        if (_sphereCollider != null)
        {
            _sphereCollider.radius = _detectionRadius;
        }

        // Обновляем настройки аудио
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        if (_audioSource != null)
        {
            _audioSource.clip = _screamClip;
            _audioSource.loop = _loopAudio;
            _audioSource.volume = _volume;
        }
    }
}

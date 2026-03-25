using System;
using UnityEngine;

/// <summary>
/// Зона периодического эхо. Реализует IVoiceWordListener.
/// При входе игрока в триггер подписывается на VoiceRecognizer.
/// Голосовая команда "light" — запускает периодическое эхо каждые N секунд.
/// Голосовая команда "dark" — останавливает периодическое эхо.
///
/// Использование:
///   1. Повесить на GameObject с Collider (isTrigger = true).
///   2. Назначить EchoPreset в инспекторе.
///   3. На сцене должен быть VoiceRecognizer и EchoManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PeriodicEchoZone : MonoBehaviour, IVoiceWordListener
{
    [Header("Эхо")]
    [Tooltip("Пресет эхо-волны")]
    [SerializeField] private EchoPreset _echoPreset;

    [Tooltip("Интервал между эхо-волнами (секунды)")]
    [SerializeField] private float _echoInterval = 3f;

    [Header("Команды")]
    [Tooltip("Слово для включения периодического эхо")]
    [SerializeField] private string _activateWord = "light";

    [Tooltip("Слово для выключения периодического эхо")]
    [SerializeField] private string _deactivateWord = "dark";

    private VoiceRecognizer _voiceRecognizer;
    private bool _isActive;
    private float _timer;
    private bool _playerInside;

    private void Update()
    {
        if (!_isActive || _echoPreset == null || EchoManager.Instance == null) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            EchoManager.Instance.SpawnEcho(transform.position, _echoPreset);
            _timer = _echoInterval;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_playerInside) return;
        if (!other.TryGetComponent<VoiceRecognizer>(out var _voiceRecognizer)) return;

        if (_voiceRecognizer == null) return;

        _voiceRecognizer.AddListener(this);
        _playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_playerInside) return;
        if (!other.TryGetComponent<VoiceRecognizer>(out var _voiceRecognizer)) return;

        if (_voiceRecognizer != null)
        {
            _voiceRecognizer.RemoveListener(this);
            _voiceRecognizer = null;
        }

        _isActive = false;
        _playerInside = false;
    }

    public void OnWordRecognized(string word)
    {
        if (string.Equals(word, _activateWord, StringComparison.OrdinalIgnoreCase))
        {
            _isActive = true;
            _timer = 0f;
        }
        else if (string.Equals(word, _deactivateWord, StringComparison.OrdinalIgnoreCase))
        {
            _isActive = false;
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_voiceRecognizer != null)
        {
            _voiceRecognizer.RemoveListener(this);
            _voiceRecognizer = null;
        }

        _isActive = false;
        _playerInside = false;
    }
}

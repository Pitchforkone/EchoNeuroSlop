using System;
using UnityEngine;
using Mirror;

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

        // Проверяем что это локальный игрок
        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity != null && !networkIdentity.isLocalPlayer) return;

        if (!other.TryGetComponent<VoiceRecognizer>(out var voiceRecognizer)) return;
        if (voiceRecognizer == null) return;

        _voiceRecognizer = voiceRecognizer;
        _voiceRecognizer.AddListener(this);
        _playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_playerInside) return;

        // Проверяем что это локальный игрок
        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity != null && !networkIdentity.isLocalPlayer) return;

        if (!other.TryGetComponent<VoiceRecognizer>(out var voiceRecognizer)) return;

        if (_voiceRecognizer != null && _voiceRecognizer == voiceRecognizer)
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

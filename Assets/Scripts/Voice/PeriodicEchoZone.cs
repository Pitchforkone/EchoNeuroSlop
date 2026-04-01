using System;
using Photon.Pun;
using UnityEngine;

public class PeriodicEchoZone : MonoBehaviour, IVoiceWordListener
{
    [Header("Эхо")]
    [Tooltip("Пресет эхо-волны")]
    [SerializeField] private EchoPreset _echoPreset;

    [Tooltip("Интервал между эхо-волнами (секунды)")]
    [SerializeField] private float _echoInterval = 3f;

    [Header("Команды")]
    [Tooltip("Слово для включения периодического эха")]
    [SerializeField] private string _activateWord = "light";

    [Tooltip("Слово для выключения периодического эха")]
    [SerializeField] private string _deactivateWord = "dark";

    [Header("Voice Zone")]
    [Tooltip("Зона активации голосовых команд")]
    [SerializeField] private VoiceActivateZoneMB _voiceActivateZone;

    private VoiceRecognizer _voiceRecognizer;
    private bool _isActive;
    private float _timer;
    private bool _playerInside;

    private void Start()
    {
        // Подписываемся на события VoiceActivateZone если она назначена
        if (_voiceActivateZone != null)
        {
            // Устанавливаем keyword для отображения (показываем оба слова)
            _voiceActivateZone.SetKeyword($"{_activateWord} / {_deactivateWord}");

            _voiceActivateZone.activate += OnVoiceZoneActivate;
            _voiceActivateZone.deactivate += OnVoiceZoneDeactivate;
        }
    }

    private void OnDestroy()
    {
        if (_voiceActivateZone != null)
        {
            _voiceActivateZone.activate -= OnVoiceZoneActivate;
            _voiceActivateZone.deactivate -= OnVoiceZoneDeactivate;
        }
    }

    private void OnVoiceZoneActivate(VoiceRecognizer voice)
    {
        if (_playerInside) return;

        _voiceRecognizer = voice;
        _voiceRecognizer.AddListener(this);
        _playerInside = true;
    }

    private void OnVoiceZoneDeactivate(VoiceRecognizer voice)
    {
        if (!_playerInside) return;

        if (_voiceRecognizer != null && _voiceRecognizer == voice)
        {
            _voiceRecognizer.RemoveListener(this);
            _voiceRecognizer = null;
        }
        _playerInside = false;
    }

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
        // Если используется VoiceActivateZone, пропускаем собственную логику триггера
        if (_voiceActivateZone != null) return;

        if (_playerInside) return;

        // Проверяем что это локальный игрок
        var pv = other.GetComponent<PhotonView>();
        if (pv != null && !pv.IsMine) return;

        if (!other.TryGetComponent<VoiceRecognizer>(out var voiceRecognizer)) return;
        if (voiceRecognizer == null) return;

        _voiceRecognizer = voiceRecognizer;
        _voiceRecognizer.AddListener(this);
        _playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        // Если используется VoiceActivateZone, пропускаем собственную логику триггера
        if (_voiceActivateZone != null) return;

        if (!_playerInside) return;

        // Проверяем что это локальный игрок
        var pvExit = other.GetComponent<PhotonView>();
        if (pvExit != null && !pvExit.IsMine) return;

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

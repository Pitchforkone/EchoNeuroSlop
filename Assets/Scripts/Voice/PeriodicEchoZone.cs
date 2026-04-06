using System;
using Mirror;
using UnityEngine;

public class PeriodicEchoZone : MonoBehaviour, IInteractable
{
    [Header("Эхо")]
    [Tooltip("Пресет эхо-волны")]
    [SerializeField] private EchoPreset _echoPreset;

    [Tooltip("Интервал между эхо-волнами (секунды)")]
    [SerializeField] private float _echoInterval = 3f;

    [Header("Interact Zone")]
    [Tooltip("Зона для определения взаимодействия")]
    [SerializeField] private InteractZone _interactZone;

    private bool _isActive;
    private float _timer;
    private bool _playerInside;

    private void Start()
    {
        if (_interactZone != null)
        {
            _interactZone.SetHintText(_isActive ? "Deactivate" : "Activate");

            _interactZone.activate += OnZoneActivate;
            _interactZone.deactivate += OnZoneDeactivate;
            _interactZone.interact += OnInteract;
        }
    }

    private void OnDestroy()
    {
        if (_interactZone != null)
        {
            _interactZone.activate -= OnZoneActivate;
            _interactZone.deactivate -= OnZoneDeactivate;
            _interactZone.interact -= OnInteract;
        }
    }

    private void OnZoneActivate()
    {
        _playerInside = true;
    }

    private void OnZoneDeactivate()
    {
        _playerInside = false;
    }

    public void OnInteract()
    {
        _isActive = !_isActive;

        if (_isActive)
        {
            _timer = 0f;
        }

        // Update hint text to reflect current state
        if (_interactZone != null)
        {
            _interactZone.SetHintText(_isActive ? "Deactivate" : "Activate");
        }
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
        // If using InteractZone, skip manual trigger handling
        if (_interactZone != null) return;

        if (_playerInside) return;

        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity != null && !networkIdentity.isLocalPlayer) return;

        _playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (_interactZone != null) return;

        if (!_playerInside) return;

        var networkIdentity = other.GetComponent<NetworkIdentity>();
        if (networkIdentity != null && !networkIdentity.isLocalPlayer) return;

        _isActive = false;
        _playerInside = false;
    }

    private void OnDisable()
    {
        _isActive = false;
        _playerInside = false;
    }
}

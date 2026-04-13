using System;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// Зона взаимодействия: когда локальный игрок находится внутри зоны
/// и нажимает клавишу E, вызывается событие interact.
/// Также поддерживает старые события activate/deactivate для совместимости.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractZone : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Подсказка, отображаемая при входе игрока в зону")]
    private string hintText = "Interact";

    /// <summary>Функция для динамического получения текста подсказки.</summary>
    private Func<string> _dynamicHintProvider;

    /// <summary>Вызывается когда игрок входит в зону.</summary>
    public Action activate;

    /// <summary>Вызывается когда игрок выходит из зоны.</summary>
    public Action deactivate;

    /// <summary>Вызывается когда игрок нажимает E внутри зоны.</summary>
    public Action interact;

    private bool _playerInside;

    private void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    public void SetHintText(string newHint)
    {
        hintText = newHint;
        _dynamicHintProvider = null;
    }

    /// <summary>
    /// Sets a dynamic hint provider that will be called each time the hint is shown.
    /// </summary>
    /// <param name="hintProvider">Function that returns the hint text.</param>
    public void SetDynamicHintProvider(Func<string> hintProvider)
    {
        _dynamicHintProvider = hintProvider;
    }

    private string GetCurrentHintText()
    {
        if (_dynamicHintProvider != null)
            return _dynamicHintProvider();
        return hintText;
    }

    private void Update()
    {
        if (!_playerInside) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            interact?.Invoke();
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (!IsLocalPlayer(other)) return;

        _playerInside = true;
        activate?.Invoke();
        InteractHintUI.Show(GetCurrentHintText());
    }

    public void OnTriggerExit(Collider other)
    {
        if (!IsLocalPlayer(other)) return;

        _playerInside = false;
        deactivate?.Invoke();
        InteractHintUI.Hide();
    }

    private bool IsLocalPlayer(Collider other)
    {
        var identity = other.GetComponent<NetworkIdentity>();
        if (identity != null)
            return identity.isLocalPlayer;

        // Offline fallback
        return other.GetComponent<NetworkBehaviour>() != null || other.CompareTag("Player");
    }
}


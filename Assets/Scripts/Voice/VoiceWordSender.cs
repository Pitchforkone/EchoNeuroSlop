using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Photon.Pun;

/// <summary>
/// Тип ввода для привязки слова.
/// </summary>
public enum InputType
{
    Keyboard,
    Mouse
}

/// <summary>
/// Кнопка мыши для привязки.
/// </summary>
public enum MouseButtonType
{
    Left,
    Right,
    Middle,
    Forward,
    Back
}

/// <summary>
/// Привязка кнопки к слову для отправки.
/// </summary>
[Serializable]
public class VoiceWordBinding
{
    public InputType inputType = InputType.Keyboard;
    public Key key = Key.V;
    public MouseButtonType mouseButton = MouseButtonType.Left;
    public string word = "привет";
}

/// <summary>
/// Компонент для отправки заранее заданных слов через VoiceRecognizer
/// по нажатию кнопок. Работает только для локального игрока.
/// </summary>
public class VoiceWordSender : MonoBehaviourPun
{
    [Header("Привязки кнопок")]
    [SerializeField] private List<VoiceWordBinding> _bindings = new List<VoiceWordBinding>
    {
        new VoiceWordBinding { inputType = InputType.Keyboard, key = Key.V, word = "привет" }
    };

    [Header("События")]
    [SerializeField] private UnityEvent<string> _onWordSent;

    public UnityEvent<string> OnWordSent => _onWordSent;
    public List<VoiceWordBinding> Bindings => _bindings;

    private Keyboard _keyboard;
    private Mouse _mouse;

    private void Awake()
    {
        _keyboard = Keyboard.current;
        _mouse = Mouse.current;
    }

    private void Update()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        if (_keyboard == null)
        {
            _keyboard = Keyboard.current;
        }

        if (_mouse == null)
        {
            _mouse = Mouse.current;
        }

        foreach (var binding in _bindings)
        {
            if (binding == null) continue;

            bool wasPressed = false;

            if (binding.inputType == InputType.Keyboard)
            {
                if (_keyboard != null && _keyboard[binding.key].wasPressedThisFrame)
                {
                    wasPressed = true;
                }
            }
            else if (binding.inputType == InputType.Mouse)
            {
                if (_mouse != null && GetMouseButton(binding.mouseButton)?.wasPressedThisFrame == true)
                {
                    wasPressed = true;
                }
            }

            if (wasPressed)
            {
                SendWord(binding.word);
            }
        }
    }

    private UnityEngine.InputSystem.Controls.ButtonControl GetMouseButton(MouseButtonType buttonType)
    {
        if (_mouse == null) return null;

        switch (buttonType)
        {
            case MouseButtonType.Left:
                return _mouse.leftButton;
            case MouseButtonType.Right:
                return _mouse.rightButton;
            case MouseButtonType.Middle:
                return _mouse.middleButton;
            case MouseButtonType.Forward:
                return _mouse.forwardButton;
            case MouseButtonType.Back:
                return _mouse.backButton;
            default:
                return null;
        }
    }

    public void SendWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            Debug.LogWarning("[VoiceWordSender] Попытка отправить пустое слово.");
            return;
        }

        VoiceRecognizer recognizer = VoiceRecognizer.LocalInstance;

        if (recognizer == null)
        {
            recognizer = GetComponent<VoiceRecognizer>();
        }

        if (recognizer == null)
        {
            Debug.LogWarning("[VoiceWordSender] VoiceRecognizer не найден.");
            return;
        }

        recognizer.SimulateWord(word);

        _onWordSent?.Invoke(word);
    }

    public void AddBinding(Key key, string word)
    {
        _bindings.Add(new VoiceWordBinding { inputType = InputType.Keyboard, key = key, word = word });
    }

    public void AddMouseBinding(MouseButtonType mouseButton, string word)
    {
        _bindings.Add(new VoiceWordBinding { inputType = InputType.Mouse, mouseButton = mouseButton, word = word });
    }

    public void RemoveBinding(int index)
    {
        if (index >= 0 && index < _bindings.Count)
        {
            _bindings.RemoveAt(index);
        }
    }

    public void ClearBindings()
    {
        _bindings.Clear();
    }
}

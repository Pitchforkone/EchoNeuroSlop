using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Mirror;

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
    [Tooltip("Тип ввода: клавиатура или мышь")]
    public InputType inputType = InputType.Keyboard;

    [Tooltip("Кнопка клавиатуры для отправки слова")]
    public Key key = Key.V;

    [Tooltip("Кнопка мыши для отправки слова")]
    public MouseButtonType mouseButton = MouseButtonType.Left;

    [Tooltip("Слово, которое будет отправлено слушателям")]
    public string word = "привет";
}

/// <summary>
/// Компонент для отправки заранее заданных слов всем слушателям VoiceRecognizer
/// по нажатию кнопок. Работает только для локального игрока.
/// 
/// Использование:
///   1. Повесьте на префаб игрока.
///   2. Настройте привязки кнопок к словам в инспекторе (_bindings).
///   3. Опционально подпишитесь на событие OnWordSent для реакции на отправку.
/// </summary>
public class VoiceWordSender : NetworkBehaviour
{
    [Header("Привязки кнопок")]
    [Tooltip("Список привязок кнопок к словам")]
    [SerializeField] private List<VoiceWordBinding> _bindings = new List<VoiceWordBinding>
    {
        new VoiceWordBinding { inputType = InputType.Keyboard, key = Key.V, word = "привет" }
    };

    [Header("События")]
    [Tooltip("Событие, вызываемое при отправке слова")]
    [SerializeField] private UnityEvent<string> _onWordSent;

    /// <summary>
    /// Событие, вызываемое при отправке слова. Можно подписаться из кода.
    /// </summary>
    public UnityEvent<string> OnWordSent => _onWordSent;

    /// <summary>
    /// Список привязок кнопок к словам.
    /// </summary>
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
        // Только для локального игрока в мультиплеере
        if (NetworkClient.active && !isLocalPlayer) return;

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

    /// <summary>
    /// Получает ButtonControl для указанной кнопки мыши.
    /// </summary>
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

    /// <summary>
    /// Отправляет указанное слово всем слушателям VoiceRecognizer.
    /// </summary>
    /// <param name="word">Слово для отправки.</param>
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
            // Попробуем найти на этом же объекте
            recognizer = GetComponent<VoiceRecognizer>();
        }

        if (recognizer == null)
        {
            Debug.LogWarning("[VoiceWordSender] VoiceRecognizer не найден. Убедитесь, что компонент VoiceRecognizer присутствует на игроке.");
            return;
        }

        // Используем публичный метод SimulateWord
        recognizer.SimulateWord(word);

        _onWordSent?.Invoke(word);
    }

    /// <summary>
    /// Добавляет новую привязку клавиши клавиатуры к слову.
    /// </summary>
    public void AddBinding(Key key, string word)
    {
        _bindings.Add(new VoiceWordBinding { inputType = InputType.Keyboard, key = key, word = word });
    }

    /// <summary>
    /// Добавляет новую привязку кнопки мыши к слову.
    /// </summary>
    public void AddMouseBinding(MouseButtonType mouseButton, string word)
    {
        _bindings.Add(new VoiceWordBinding { inputType = InputType.Mouse, mouseButton = mouseButton, word = word });
    }

    /// <summary>
    /// Удаляет привязку по индексу.
    /// </summary>
    public void RemoveBinding(int index)
    {
        if (index >= 0 && index < _bindings.Count)
        {
            _bindings.RemoveAt(index);
        }
    }

    /// <summary>
    /// Очищает все привязки.
    /// </summary>
    public void ClearBindings()
    {
        _bindings.Clear();
    }
}

using UnityEngine;
using UnityEngine.UI;
using Mirror;

/// <summary>
/// UI-индикатор голосового чата.
/// Показывает иконку микрофона когда локальный игрок говорит (передаёт голос),
/// и иконки-индикаторы над головами удалённых игроков, которые говорят.
///
/// Вешается на префаб игрока рядом с VoiceChat.
/// Автоматически создаёт World-Space Canvas для индикатора над головой.
/// </summary>
public class VoiceChatUI : MonoBehaviour
{
    [Header("Индикатор над головой (для удалённых игроков)")]
    [Tooltip("Высота индикатора над центром объекта")]
    [SerializeField] private float _indicatorHeight = 2.3f;

    [Tooltip("Размер иконки индикатора")]
    [SerializeField] private float _indicatorSize = 0.3f;

    [Header("HUD-иконка (для локального игрока)")]
    [Tooltip("Готовый Image на HUD Canvas для иконки микрофона. " +
             "Если не указан — будет создан автоматически.")]
    [SerializeField] private Image _hudMicIcon;

    [Tooltip("Смещение HUD-иконки от левого нижнего угла")]
    [SerializeField] private Vector2 _hudOffset = new Vector2(60f, 60f);

    [Tooltip("Размер HUD-иконки")]
    [SerializeField] private float _hudIconSize = 40f;

    [Header("Цвета")]
    [SerializeField] private Color _speakingColor = new Color(0.2f, 1f, 0.3f, 1f);
    [SerializeField] private Color _mutedColor = new Color(1f, 0.3f, 0.3f, 0.6f);
    [SerializeField] private Color _idleColor = new Color(1f, 1f, 1f, 0.3f);

    private VoiceChat _voiceChat;
    private bool _isLocalPlayer;

    // World-space indicator (для удалённых)
    private Canvas _worldCanvas;
    private Image _worldIcon;

    // HUD (для локального)
    private Canvas _hudCanvas;
    private Image _createdHudIcon;

    private void Start()
    {
        _voiceChat = GetComponent<VoiceChat>();
        if (_voiceChat == null)
        {
            enabled = false;
            return;
        }

        var netIdentity = GetComponent<NetworkIdentity>();
        _isLocalPlayer = netIdentity != null && netIdentity.isLocalPlayer;

        // Если нет Mirror — считаем локальным
        if (!NetworkClient.active)
            _isLocalPlayer = true;

        if (_isLocalPlayer)
        {
            SetupHUD();
        }
        else
        {
            SetupWorldIndicator();
        }

        _voiceChat.OnTransmitStateChanged += OnTransmitChanged;
        _voiceChat.OnPlaybackStateChanged += OnPlaybackChanged;
    }

    // ??????????????????????? Индикатор над головой ???????????????????????

    private void SetupWorldIndicator()
    {
        GameObject canvasGO = new GameObject("VoiceIndicatorCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, _indicatorHeight, 0f);

        _worldCanvas = canvasGO.AddComponent<Canvas>();
        _worldCanvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = _worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(_indicatorSize, _indicatorSize);
        canvasRect.localScale = Vector3.one;

        // Иконка
        GameObject iconGO = new GameObject("MicIcon");
        iconGO.transform.SetParent(canvasGO.transform, false);

        RectTransform iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.sizeDelta = Vector2.zero;
        iconRect.anchoredPosition = Vector2.zero;

        _worldIcon = iconGO.AddComponent<Image>();
        _worldIcon.color = _idleColor;

        // Скрываем по умолчанию
        canvasGO.SetActive(false);
    }

    private void LateUpdate()
    {
        // Billboard — поворачиваем Canvas лицом к камере
        if (_worldCanvas != null && Camera.main != null)
        {
            _worldCanvas.transform.forward = Camera.main.transform.forward;
        }
    }

    // ??????????????????????? HUD-иконка ???????????????????????

    private void SetupHUD()
    {
        if (_hudMicIcon != null)
        {
            _hudMicIcon.color = _idleColor;
            _hudMicIcon.gameObject.SetActive(true);
            return;
        }

        // Создаём Screen-Space Canvas
        GameObject canvasGO = new GameObject("VoiceChatHUD");
        canvasGO.transform.SetParent(transform);

        _hudCanvas = canvasGO.AddComponent<Canvas>();
        _hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _hudCanvas.sortingOrder = 99;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Иконка микрофона (текст-заглушка "MIC")
        GameObject iconGO = new GameObject("MicHudIcon");
        iconGO.transform.SetParent(canvasGO.transform, false);

        RectTransform iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(0f, 0f);
        iconRect.pivot = new Vector2(0f, 0f);
        iconRect.anchoredPosition = _hudOffset;
        iconRect.sizeDelta = new Vector2(_hudIconSize, _hudIconSize);

        _createdHudIcon = iconGO.AddComponent<Image>();
        _createdHudIcon.color = _idleColor;

        // Текст "MIC" поверх иконки
        GameObject textGO = new GameObject("MicLabel");
        textGO.transform.SetParent(iconGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        Text label = textGO.AddComponent<Text>();
        label.text = "MIC";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 14;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
    }

    // ??????????????????????? Обработка событий ???????????????????????

    private void OnTransmitChanged(bool isSpeaking)
    {
        if (!_isLocalPlayer) return;

        Image icon = _hudMicIcon != null ? _hudMicIcon : _createdHudIcon;
        if (icon != null)
        {
            icon.color = isSpeaking ? _speakingColor : _idleColor;
        }
    }

    private void OnPlaybackChanged(bool isPlaying)
    {
        if (_isLocalPlayer) return;

        if (_worldCanvas != null)
        {
            _worldCanvas.gameObject.SetActive(isPlaying);
        }

        if (_worldIcon != null)
        {
            _worldIcon.color = isPlaying ? _speakingColor : _idleColor;
        }
    }

    // ??????????????????????? Публичные методы ???????????????????????

    /// <summary>
    /// Обновить цвет иконки вручную (например, при mute).
    /// </summary>
    public void SetMutedVisual(bool muted)
    {
        Image icon = _hudMicIcon != null ? _hudMicIcon : _createdHudIcon;
        if (icon != null && _isLocalPlayer)
        {
            icon.color = muted ? _mutedColor : _idleColor;
        }
    }

    private void OnDestroy()
    {
        if (_voiceChat != null)
        {
            _voiceChat.OnTransmitStateChanged -= OnTransmitChanged;
            _voiceChat.OnPlaybackStateChanged -= OnPlaybackChanged;
        }

        if (_hudCanvas != null)
            Destroy(_hudCanvas.gameObject);

        if (_worldCanvas != null)
            Destroy(_worldCanvas.gameObject);
    }
}

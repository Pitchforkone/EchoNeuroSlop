using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI компонент для отображения громкости микрофона в виде полоски (HP бар стиль).
/// Полоска физически растягивается/сжимается в зависимости от громкости.
/// Автоматически создаёт Canvas с UI элементами.
/// Использует SharedMicrophone для получения аудио данных.
/// 
/// ВАЖНО: Этот компонент только читает данные, не влияет на работу VoiceRecognizer.
/// 
/// Использование:
///   1. Добавьте этот компонент на любой GameObject в сцене.
///   2. При запуске автоматически создастся Canvas с полоской громкости.
///   3. Или назначьте существующие UI элементы через инспектор.
/// </summary>
public class MicrophoneVolumeUI : MonoBehaviour
{
    [Header("UI References (Optional - will auto-create if not assigned)")]
    [Tooltip("Image компонент для отображения громкости (будет растягиваться по ширине)")]
    [SerializeField] private Image _volumeBar;
    
    [Tooltip("RectTransform полоски громкости")]
    [SerializeField] private RectTransform _volumeBarRect;
    
    [Tooltip("Фоновый Image для полоски громкости")]
    [SerializeField] private Image _backgroundBar;
    
    [Tooltip("Canvas для UI (создастся автоматически если не назначен)")]
    [SerializeField] private Canvas _canvas;
    
    [Header("Settings")]
    [Tooltip("Количество сэмплов для анализа громкости")]
    [SerializeField] private int _sampleSize = 128;
    
    [Tooltip("Множитель чувствительности микрофона")]
    [SerializeField] private float _sensitivity = 100f;
    
    [Tooltip("Скорость плавного уменьшения громкости")]
    [SerializeField] private float _smoothSpeed = 5f;
    
    [Tooltip("Минимальный порог громкости для отображения")]
    [SerializeField] private float _minThreshold = 0.01f;
    
    [Tooltip("Интервал обновления громкости (секунды). Меньше = чаще обновляется")]
    [SerializeField] private float _updateInterval = 0.05f;
    
    [Header("Visual Settings")]
    [Tooltip("Цвет полоски при низкой громкости")]
    [SerializeField] private Color _lowVolumeColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    
    [Tooltip("Цвет полоски при средней громкости")]
    [SerializeField] private Color _midVolumeColor = new Color(1f, 0.8f, 0.2f, 1f);
    
    [Tooltip("Цвет полоски при высокой громкости")]
    [SerializeField] private Color _highVolumeColor = new Color(1f, 0.2f, 0.2f, 1f);
    
    [Tooltip("Максимальная ширина полоски (при 100% громкости)")]
    [SerializeField] private float _barMaxWidth = 200f;
    
    [Tooltip("Высота полоски")]
    [SerializeField] private float _barHeight = 20f;
    
    [Tooltip("Отступ от нижнего края экрана")]
    [SerializeField] private float _bottomOffset = 50f;

    private float[] _samples;
    private float _currentVolume;
    private float _smoothedVolume;
    private bool _isInitialized;
    private float _lastUpdateTime;

    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        _samples = new float[_sampleSize];
        
        // Если Canvas не назначен, создаём новый
        if (_canvas == null)
        {
            CreateCanvas();
        }
        
        // Если полоска не назначена, создаём UI элементы
        if (_volumeBar == null)
        {
            CreateVolumeBarUI();
        }
        
        _isInitialized = true;
    }

    private void CreateCanvas()
    {
        // Создаём новый GameObject для Canvas
        GameObject canvasGO = new GameObject("MicrophoneVolumeCanvas");
        canvasGO.transform.SetParent(transform);
        
        // Добавляем Canvas компонент
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100; // Поверх других UI
        
        // Добавляем CanvasScaler для масштабирования
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        // Добавляем GraphicRaycaster (требуется для Canvas)
        canvasGO.AddComponent<GraphicRaycaster>();
    }

    private void CreateVolumeBarUI()
    {
        // Создаём контейнер для полоски
        GameObject containerGO = new GameObject("VolumeBarContainer");
        containerGO.transform.SetParent(_canvas.transform, false);
        
        RectTransform containerRect = containerGO.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0f, _bottomOffset);
        containerRect.sizeDelta = new Vector2(_barMaxWidth, _barHeight);
        
        // Создаём фоновый Image (полная ширина - показывает максимум)
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(containerGO.transform, false);
        
        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        _backgroundBar = bgGO.AddComponent<Image>();
        _backgroundBar.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        
        // Создаём полоску громкости (растягивается по ширине)
        GameObject fillGO = new GameObject("VolumeFill");
        fillGO.transform.SetParent(containerGO.transform, false);
        
        _volumeBarRect = fillGO.AddComponent<RectTransform>();
        // Привязываем к левому краю контейнера
        _volumeBarRect.anchorMin = new Vector2(0f, 0f);
        _volumeBarRect.anchorMax = new Vector2(0f, 1f);
        _volumeBarRect.pivot = new Vector2(0f, 0.5f);
        _volumeBarRect.anchoredPosition = Vector2.zero;
        // Начинаем с нулевой ширины
        _volumeBarRect.sizeDelta = new Vector2(0f, 0f);
        
        _volumeBar = fillGO.AddComponent<Image>();
        _volumeBar.color = _lowVolumeColor;
        
        // Добавляем рамку вокруг контейнера
        GameObject borderGO = new GameObject("Border");
        borderGO.transform.SetParent(containerGO.transform, false);
        
        RectTransform borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        borderRect.anchoredPosition = Vector2.zero;
        
        Image borderImage = borderGO.AddComponent<Image>();
        borderImage.color = Color.clear;
        
        // Используем Outline для рамки
        Outline outline = borderGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.5f);
        outline.effectDistance = new Vector2(2f, 2f);
        
        // Создаём иконку микрофона (текст)
        GameObject iconGO = new GameObject("MicIcon");
        iconGO.transform.SetParent(containerGO.transform, false);
        
        RectTransform iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(1f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-10f, 0f);
        iconRect.sizeDelta = new Vector2(30f, 30f);
        
        Text iconText = iconGO.AddComponent<Text>();
        iconText.text = "MIC";
        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iconText.fontSize = 12;
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.color = Color.white;
    }

    private void Update()
    {
        if (!_isInitialized) return;
        
        // Обновляем громкость с заданным интервалом (чтобы не нагружать систему)
        if (Time.time - _lastUpdateTime >= _updateInterval)
        {
            _lastUpdateTime = Time.time;
            UpdateVolume();
        }
        
        // UI обновляем каждый кадр для плавной анимации
        UpdateUI();
    }

    private void UpdateVolume()
    {
        SharedMicrophone mic = SharedMicrophone.LocalInstance;
        
        if (mic == null || !mic.IsRecording || mic.Clip == null)
        {
            _currentVolume = 0f;
            return;
        }
        
        AudioClip clip = mic.Clip;
        
        // Проверяем валидность клипа
        if (clip.channels <= 0 || clip.samples <= 0)
        {
            _currentVolume = 0f;
            return;
        }
        
        // Получаем текущую позицию записи
        int currentPosition = mic.GetPosition();
        
        // Защита от невалидной позиции
        if (currentPosition < 0 || currentPosition >= clip.samples)
        {
            _currentVolume = 0f;
            return;
        }
        
        // Вычисляем безопасную позицию для чтения
        // Читаем немного позади текущей позиции, чтобы не конфликтовать с записью
        int samplesToRead = Mathf.Min(_sampleSize, clip.samples / 2);
        int readPosition = currentPosition - samplesToRead - 64; // небольшой отступ
        
        if (readPosition < 0)
        {
            readPosition += clip.samples;
        }
        
        // Убеждаемся что позиция в допустимых пределах
        readPosition = Mathf.Clamp(readPosition, 0, clip.samples - samplesToRead);
        
        // Проверяем размер буфера
        if (_samples == null || _samples.Length != samplesToRead)
        {
            _samples = new float[samplesToRead];
        }
        
        try
        {
            // Читаем сэмплы из AudioClip
            clip.GetData(_samples, readPosition);
            
            // Вычисляем RMS (Root Mean Square) для громкости
            float sum = 0f;
            for (int i = 0; i < _samples.Length; i++)
            {
                sum += _samples[i] * _samples[i];
            }
            
            float rms = Mathf.Sqrt(sum / _samples.Length);
            _currentVolume = Mathf.Clamp01(rms * _sensitivity);
            
            // Применяем минимальный порог
            if (_currentVolume < _minThreshold)
            {
                _currentVolume = 0f;
            }
        }
        catch (System.Exception)
        {
            // Игнорируем ошибки чтения (может произойти при старте или смене устройства)
            _currentVolume = 0f;
        }
    }

    private void UpdateUI()
    {
        if (_volumeBar == null || _volumeBarRect == null) return;
        
        // Плавно интерполируем громкость
        // Быстро поднимается, медленно опускается
        if (_currentVolume > _smoothedVolume)
        {
            _smoothedVolume = _currentVolume;
        }
        else
        {
            _smoothedVolume = Mathf.Lerp(_smoothedVolume, _currentVolume, Time.deltaTime * _smoothSpeed);
        }
        
        // Обновляем ширину полоски (HP бар стиль)
        float targetWidth = _smoothedVolume * _barMaxWidth;
        Vector2 currentSize = _volumeBarRect.sizeDelta;
        _volumeBarRect.sizeDelta = new Vector2(targetWidth, currentSize.y);
        
        // Обновляем цвет в зависимости от громкости
        Color targetColor;
        if (_smoothedVolume < 0.33f)
        {
            targetColor = Color.Lerp(_lowVolumeColor, _midVolumeColor, _smoothedVolume / 0.33f);
        }
        else if (_smoothedVolume < 0.66f)
        {
            targetColor = Color.Lerp(_midVolumeColor, _highVolumeColor, (_smoothedVolume - 0.33f) / 0.33f);
        }
        else
        {
            targetColor = _highVolumeColor;
        }
        
        _volumeBar.color = targetColor;
    }

    /// <summary>
    /// Возвращает текущую нормализованную громкость (0-1).
    /// </summary>
    public float GetCurrentVolume()
    {
        return _smoothedVolume;
    }

    /// <summary>
    /// Возвращает текущую сырую громкость без сглаживания (0-1).
    /// </summary>
    public float GetRawVolume()
    {
        return _currentVolume;
    }

    /// <summary>
    /// Устанавливает видимость UI.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_canvas != null)
        {
            _canvas.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Возвращает true, если микрофон активен и записывает.
    /// </summary>
    public bool IsMicrophoneActive()
    {
        SharedMicrophone mic = SharedMicrophone.LocalInstance;
        return mic != null && mic.IsRecording;
    }

    private void OnDestroy()
    {
        // Очистка ресурсов
        if (_canvas != null && _canvas.transform.parent == transform)
        {
            Destroy(_canvas.gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Проверки в редакторе
        if (_sampleSize < 32) _sampleSize = 32;
        if (_sampleSize > 1024) _sampleSize = 1024;
        if (_sensitivity < 1f) _sensitivity = 1f;
        if (_barMaxWidth < 50f) _barMaxWidth = 50f;
        if (_barHeight < 5f) _barHeight = 5f;
        if (_updateInterval < 0.01f) _updateInterval = 0.01f;
    }
#endif
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI компонент для отображения громкости микрофона в виде полоски (HP бар стиль).
/// Полоска физически растягивается/сжимается в зависимости от громкости.
/// Автоматически создаёт Canvas с UI элементами.
/// Использует SharedMicrophone для получения аудио данных.
/// </summary>
public class MicrophoneVolumeUI : MonoBehaviour
{
    [Header("UI References (Optional - will auto-create if not assigned)")]
    [SerializeField] private Image _volumeBar;
    [SerializeField] private RectTransform _volumeBarRect;
    [SerializeField] private Image _backgroundBar;
    [SerializeField] private Canvas _canvas;
    
    [Header("Settings")]
    [SerializeField] private int _sampleSize = 128;
    [SerializeField] private float _sensitivity = 100f;
    [SerializeField] private float _smoothSpeed = 5f;
    [SerializeField] private float _minThreshold = 0.01f;
    [SerializeField] private float _updateInterval = 0.05f;
    
    [Header("Visual Settings")]
    [SerializeField] private Color _lowVolumeColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color _midVolumeColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color _highVolumeColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float _barMaxWidth = 200f;
    [SerializeField] private float _barHeight = 20f;
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
        
        if (_canvas == null)
            CreateCanvas();
        
        if (_volumeBar == null)
            CreateVolumeBarUI();
        
        _isInitialized = true;
    }

    private void CreateCanvas()
    {
        GameObject canvasGO = new GameObject("MicrophoneVolumeCanvas");
        canvasGO.transform.SetParent(transform);
        
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasGO.AddComponent<GraphicRaycaster>();
    }

    private void CreateVolumeBarUI()
    {
        GameObject containerGO = new GameObject("VolumeBarContainer");
        containerGO.transform.SetParent(_canvas.transform, false);
        
        RectTransform containerRect = containerGO.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0f, _bottomOffset);
        containerRect.sizeDelta = new Vector2(_barMaxWidth, _barHeight);
        
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(containerGO.transform, false);
        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        _backgroundBar = bgGO.AddComponent<Image>();
        _backgroundBar.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        
        GameObject fillGO = new GameObject("VolumeFill");
        fillGO.transform.SetParent(containerGO.transform, false);
        _volumeBarRect = fillGO.AddComponent<RectTransform>();
        _volumeBarRect.anchorMin = new Vector2(0f, 0f);
        _volumeBarRect.anchorMax = new Vector2(0f, 1f);
        _volumeBarRect.pivot = new Vector2(0f, 0.5f);
        _volumeBarRect.anchoredPosition = Vector2.zero;
        _volumeBarRect.sizeDelta = new Vector2(0f, 0f);
        _volumeBar = fillGO.AddComponent<Image>();
        _volumeBar.color = _lowVolumeColor;
        
        GameObject borderGO = new GameObject("Border");
        borderGO.transform.SetParent(containerGO.transform, false);
        RectTransform borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        borderRect.anchoredPosition = Vector2.zero;
        Image borderImage = borderGO.AddComponent<Image>();
        borderImage.color = Color.clear;
        Outline outline = borderGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.5f);
        outline.effectDistance = new Vector2(2f, 2f);
        
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
        
        if (Time.time - _lastUpdateTime >= _updateInterval)
        {
            _lastUpdateTime = Time.time;
            UpdateVolume();
        }
        
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
        
        if (clip.channels <= 0 || clip.samples <= 0)
        {
            _currentVolume = 0f;
            return;
        }
        
        int currentPosition = mic.GetPosition();
        
        if (currentPosition < 0 || currentPosition >= clip.samples)
        {
            _currentVolume = 0f;
            return;
        }
        
        int samplesToRead = Mathf.Min(_sampleSize, clip.samples / 2);
        int readPosition = currentPosition - samplesToRead - 64;
        
        if (readPosition < 0)
            readPosition += clip.samples;
        
        readPosition = Mathf.Clamp(readPosition, 0, clip.samples - samplesToRead);
        
        if (_samples == null || _samples.Length != samplesToRead)
            _samples = new float[samplesToRead];
        
        try
        {
            clip.GetData(_samples, readPosition);
            
            float sum = 0f;
            for (int i = 0; i < _samples.Length; i++)
            {
                sum += _samples[i] * _samples[i];
            }
            
            float rms = Mathf.Sqrt(sum / _samples.Length);
            _currentVolume = Mathf.Clamp01(rms * _sensitivity);
            
            if (_currentVolume < _minThreshold)
                _currentVolume = 0f;
        }
        catch (System.Exception)
        {
            _currentVolume = 0f;
        }
    }

    private void UpdateUI()
    {
        if (_volumeBar == null || _volumeBarRect == null) return;
        
        if (_currentVolume > _smoothedVolume)
            _smoothedVolume = _currentVolume;
        else
            _smoothedVolume = Mathf.Lerp(_smoothedVolume, _currentVolume, Time.deltaTime * _smoothSpeed);
        
        float targetWidth = _smoothedVolume * _barMaxWidth;
        Vector2 currentSize = _volumeBarRect.sizeDelta;
        _volumeBarRect.sizeDelta = new Vector2(targetWidth, currentSize.y);
        
        Color targetColor;
        if (_smoothedVolume < 0.33f)
            targetColor = Color.Lerp(_lowVolumeColor, _midVolumeColor, _smoothedVolume / 0.33f);
        else if (_smoothedVolume < 0.66f)
            targetColor = Color.Lerp(_midVolumeColor, _highVolumeColor, (_smoothedVolume - 0.33f) / 0.33f);
        else
            targetColor = _highVolumeColor;
        
        _volumeBar.color = targetColor;
    }

    public float GetCurrentVolume() => _smoothedVolume;
    public float GetRawVolume() => _currentVolume;

    public void SetVisible(bool visible)
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(visible);
    }

    public bool IsMicrophoneActive()
    {
        SharedMicrophone mic = SharedMicrophone.LocalInstance;
        return mic != null && mic.IsRecording;
    }

    private void OnDestroy()
    {
        if (_canvas != null && _canvas.transform.parent == transform)
            Destroy(_canvas.gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_sampleSize < 32) _sampleSize = 32;
        if (_sampleSize > 1024) _sampleSize = 1024;
        if (_sensitivity < 1f) _sensitivity = 1f;
        if (_barMaxWidth < 50f) _barMaxWidth = 50f;
        if (_barHeight < 5f) _barHeight = 5f;
        if (_updateInterval < 0.01f) _updateInterval = 0.01f;
    }
#endif
}

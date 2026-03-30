using UnityEngine;

/// <summary>
/// Компонент экрана для отображения изображения с камеры видеонаблюдения.
/// Один монитор показывает одну камеру.
/// </summary>
public class SecurityMonitor : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Renderer _screenRenderer;
    [SerializeField] private Material _screenMaterial;
    
    [Header("Camera")]
    [Tooltip("Камера видеонаблюдения для этого монитора")]
    [SerializeField] private SecurityCamera _camera;
    
    [Header("Power")]
    [SerializeField] private bool _isPowered = true;
    [SerializeField] private Color _offColor = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private Color _noSignalColor = new Color(0f, 0f, 0.2f);
    [SerializeField] private Texture2D _noSignalTexture;

    private Material _instanceMaterial;
    private RenderTexture _cameraFeed;

    public bool IsPowered
    {
        get => _isPowered;
        set
        {
            _isPowered = value;
            UpdateDisplay();
        }
    }

    public SecurityCamera Camera => _camera;

    private void Awake()
    {
        // Автоматически получаем Renderer если не назначен
        if (_screenRenderer == null)
            _screenRenderer = GetComponent<Renderer>();
        
        if (_screenRenderer == null)
            _screenRenderer = GetComponentInChildren<Renderer>();
    }

    private void Start()
    {
        SetupMaterial();
        SetupCameraFeed();
        UpdateDisplay();
    }

    private void SetupMaterial()
    {
        if (_screenRenderer == null)
        {
            Debug.LogError($"[SecurityMonitor] Renderer не найден на {gameObject.name}!");
            return;
        }

        // ВАЖНО: Всегда создаём новый экземпляр материала для каждого монитора
        // чтобы изменения текстуры не влияли на другие мониторы
        Material sourceMaterial = _screenMaterial != null ? _screenMaterial : _screenRenderer.sharedMaterial;
        
        if (sourceMaterial != null)
        {
            _instanceMaterial = new Material(sourceMaterial);
            _instanceMaterial.name = $"{sourceMaterial.name}_Instance_{gameObject.name}";
            _screenRenderer.material = _instanceMaterial;
        }
        else
        {
            Debug.LogError($"[SecurityMonitor] Нет материала на {gameObject.name}!");
        }
    }

    private void SetupCameraFeed()
    {
        if (_camera != null)
        {
            _cameraFeed = _camera.GetRenderTexture();
            
            if (_cameraFeed == null)
            {
                Debug.LogWarning($"[SecurityMonitor] Камера {_camera.name} не имеет RenderTexture!");
            }
        }
        else
        {
            Debug.LogWarning($"[SecurityMonitor] Камера не назначена на {gameObject.name}!");
        }
    }

    private void UpdateDisplay()
    {
        if (_screenRenderer == null || _instanceMaterial == null) return;

        if (!_isPowered)
        {
            // Монитор выключен
            _instanceMaterial.mainTexture = Texture2D.blackTexture;
            _instanceMaterial.color = _offColor;
        }
        else if (_cameraFeed == null)
        {
            // Нет сигнала
            _instanceMaterial.mainTexture = _noSignalTexture != null ? _noSignalTexture : Texture2D.grayTexture;
            _instanceMaterial.color = _noSignalColor;
        }
        else
        {
            // Нормальное отображение
            _instanceMaterial.mainTexture = _cameraFeed;
            _instanceMaterial.color = Color.white;
        }
    }

    /// <summary>
    /// Устанавливает камеру для этого монитора.
    /// </summary>
    public void SetCamera(SecurityCamera camera)
    {
        _camera = camera;
        SetupCameraFeed();
        UpdateDisplay();
    }

    /// <summary>
    /// Переключает питание монитора.
    /// </summary>
    public void TogglePower()
    {
        IsPowered = !IsPowered;
    }

    private void OnDestroy()
    {
        // Уничтожаем созданный экземпляр материала
        if (_instanceMaterial != null)
        {
            Destroy(_instanceMaterial);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_screenRenderer == null)
        {
            _screenRenderer = GetComponent<Renderer>();
            if (_screenRenderer == null)
                _screenRenderer = GetComponentInChildren<Renderer>();
        }
    }
#endif
}

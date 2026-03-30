using UnityEngine;

/// <summary>
/// Камера видеонаблюдения с возможностью вращения и эффектами.
/// </summary>
public class SecurityCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera _securityCamera;
    [SerializeField] private RenderTexture _renderTexture;
    
    [Header("Rotation")]
    [SerializeField] private bool _enableRotation = false;  // По умолчанию выключено
    [SerializeField] private float _rotationSpeed = 20f;
    [SerializeField] private float _rotationAngle = 45f;
    
    [Header("Screen Effect")]
    [SerializeField] private Material _screenMaterial;
    [SerializeField] private bool _enableScanlines = true;
    [SerializeField] private bool _enableNoise = true;
    [SerializeField] [Range(0f, 1f)] private float _noiseIntensity = 0.1f;
    
    [Header("Power")]
    [SerializeField] private bool _isPowered = true;
    [SerializeField] private Color _offScreenColor = Color.black;

    private float _currentAngle;
    private float _rotationDirection = 1f;
    private Vector3 _initialRotation;
    private Renderer _screenRenderer;

    public bool IsPowered
    {
        get => _isPowered;
        set
        {
            _isPowered = value;
            UpdatePowerState();
        }
    }

    /// <summary>
    /// Возвращает RenderTexture этой камеры.
    /// </summary>
    public RenderTexture GetRenderTexture()
    {
        return _renderTexture;
    }

    private void Awake()
    {
        if (_securityCamera == null)
            _securityCamera = GetComponentInChildren<Camera>();

        _initialRotation = transform.localEulerAngles;

        // Убедимся, что у камеры нет AudioListener
        var audioListener = _securityCamera?.GetComponent<AudioListener>();
        if (audioListener != null)
            audioListener.enabled = false;
    }

    private void Start()
    {
        SetupRenderTexture();
        UpdatePowerState();
    }

    private void Update()
    {
        if (_enableRotation && _isPowered)
        {
            UpdateRotation();
        }
    }

    private void SetupRenderTexture()
    {
        if (_securityCamera != null && _renderTexture != null)
        {
            _securityCamera.targetTexture = _renderTexture;
            Debug.Log($"[SecurityCamera] {gameObject.name} настроена с RenderTexture: {_renderTexture.name}");
        }
        else
        {
            if (_securityCamera == null)
                Debug.LogError($"[SecurityCamera] Камера не назначена на {gameObject.name}!");
            if (_renderTexture == null)
                Debug.LogError($"[SecurityCamera] RenderTexture не назначена на {gameObject.name}!");
        }
    }

    private void UpdateRotation()
    {
        _currentAngle += _rotationSpeed * _rotationDirection * Time.deltaTime;

        if (Mathf.Abs(_currentAngle) >= _rotationAngle)
        {
            _currentAngle = Mathf.Sign(_currentAngle) * _rotationAngle;
            _rotationDirection *= -1f;
        }

        transform.localEulerAngles = _initialRotation + new Vector3(0f, _currentAngle, 0f);
    }

    private void UpdatePowerState()
    {
        if (_securityCamera != null)
        {
            _securityCamera.enabled = _isPowered;
        }

        // Обновляем материал экрана если камера выключена
        if (_screenMaterial != null)
        {
            if (_isPowered)
            {
                _screenMaterial.mainTexture = _renderTexture;
            }
            else
            {
                _screenMaterial.mainTexture = null;
                _screenMaterial.color = _offScreenColor;
            }
        }
    }

    /// <summary>
    /// Устанавливает экран для отображения изображения с камеры.
    /// </summary>
    public void SetScreen(Renderer screenRenderer)
    {
        _screenRenderer = screenRenderer;
        if (_screenRenderer != null && _renderTexture != null)
        {
            _screenRenderer.material.mainTexture = _renderTexture;
        }
    }

    /// <summary>
    /// Переключает питание камеры.
    /// </summary>
    public void TogglePower()
    {
        IsPowered = !IsPowered;
    }

    private void OnDestroy()
    {
        // Освобождаем Render Texture если она была создана динамически
        if (_renderTexture != null && !_renderTexture.IsCreated())
        {
            _renderTexture.Release();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Визуализация угла обзора камеры
        if (_securityCamera != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = _securityCamera.transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, _securityCamera.fieldOfView, 
                _securityCamera.farClipPlane, _securityCamera.nearClipPlane, 
                _securityCamera.aspect);
        }

        // Визуализация угла вращения
        if (_enableRotation)
        {
            Gizmos.color = Color.yellow;
            Vector3 leftDir = Quaternion.Euler(0, -_rotationAngle, 0) * transform.forward;
            Vector3 rightDir = Quaternion.Euler(0, _rotationAngle, 0) * transform.forward;
            Gizmos.DrawRay(transform.position, leftDir * 3f);
            Gizmos.DrawRay(transform.position, rightDir * 3f);
        }
    }
#endif
}

using UnityEngine;

/// <summary>
/// Простой компонент для отображения Render Texture на экране.
/// Автоматически создаёт материал при старте.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class SimpleSecurityScreen : MonoBehaviour
{
    [Header("Camera Feed")]
    [Tooltip("Render Texture от камеры видеонаблюдения")]
    [SerializeField] private RenderTexture _cameraRenderTexture;
    
    [Header("Settings")]
    [Tooltip("Использовать Unlit шейдер (экран будет светиться)")]
    [SerializeField] private bool _useUnlitShader = true;
    
    private Renderer _renderer;
    private Material _screenMaterial;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        SetupScreen();
    }

    private void SetupScreen()
    {
        if (_cameraRenderTexture == null)
        {
            Debug.LogWarning($"[SimpleSecurityScreen] Render Texture не назначена на {gameObject.name}!");
            return;
        }

        // Создаём материал с подходящим шейдером
        Shader shader = null;
        
        if (_useUnlitShader)
        {
            // Пробуем найти URP Unlit шейдер
            shader = Shader.Find("Universal Render Pipeline/Unlit");
            
            // Если не нашли - используем стандартный Unlit
            if (shader == null)
                shader = Shader.Find("Unlit/Texture");
        }
        
        // Fallback на стандартный шейдер
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError("[SimpleSecurityScreen] Не удалось найти подходящий шейдер!");
            return;
        }

        _screenMaterial = new Material(shader);
        _screenMaterial.mainTexture = _cameraRenderTexture;
        
        // Для URP Unlit нужно установить _BaseMap
        if (shader.name.Contains("Universal"))
        {
            _screenMaterial.SetTexture("_BaseMap", _cameraRenderTexture);
        }

        _renderer.material = _screenMaterial;
        
        Debug.Log($"[SimpleSecurityScreen] Экран {gameObject.name} настроен с шейдером: {shader.name}");
    }

    /// <summary>
    /// Устанавливает новую Render Texture во время игры.
    /// </summary>
    public void SetRenderTexture(RenderTexture renderTexture)
    {
        _cameraRenderTexture = renderTexture;
        
        if (_screenMaterial != null)
        {
            _screenMaterial.mainTexture = renderTexture;
            
            // Для URP
            if (_screenMaterial.HasProperty("_BaseMap"))
            {
                _screenMaterial.SetTexture("_BaseMap", renderTexture);
            }
        }
    }

    private void OnDestroy()
    {
        // Уничтожаем созданный материал
        if (_screenMaterial != null)
        {
            Destroy(_screenMaterial);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Подсказка в редакторе
        if (_cameraRenderTexture == null)
        {
            Debug.LogWarning($"[SimpleSecurityScreen] Назначьте Render Texture на {gameObject.name}");
        }
    }
#endif
}

using System.Collections;
using UnityEngine;
using Mirror;

/// <summary>
/// Контроллер очков ночного видения.
/// Слушает событие NightVisionItem.OnNightVisionUsed и включает свет на заданное время.
/// Использует механизм затухания из LightDestroy.
/// 
/// Использование:
///   1. Подберите очки ночного видения (PickupableItem с типом NightVision).
///   2. Скажите "glasses" для активации.
///   3. Свет будет включён на указанное время, затем плавно погаснет.
/// </summary>
public class NightVisionController : NetworkBehaviour
{
    [Header("Настройки света")]
    [Tooltip("Префаб света для ночного видения (должен содержать Light компонент)")]
    [SerializeField] private GameObject _lightPrefab;
    
    [Tooltip("Интенсивность света")]
    [SerializeField] private float _lightIntensity = 2f;
    
    [Tooltip("Дальность света")]
    [SerializeField] private float _lightRange = 50f;
    
    [Tooltip("Цвет света (зеленоватый для ночного видения)")]
    [SerializeField] private Color _lightColor = new Color(0.2f, 1f, 0.3f);
    
    [Header("Настройки времени")]
    [Tooltip("Время работы очков ночного видения (секунды)")]
    [SerializeField] private float _activeDuration = 10f;
    
    [Tooltip("Время затухания света (секунды)")]
    [SerializeField] private float _fadeDuration = 3f;
    
    [Header("Кулдаун")]
    [Tooltip("Время перезарядки между использованиями (секунды)")]
    [SerializeField] private float _cooldown = 1f;
    
    private GameObject _currentLight;
    private Light _lightComponent;
    private float _lastUseTime = -999f;
    private bool _isInitialized = false;
    private bool _isActive = false;
    private Coroutine _activeCoroutine;
    
    private void Start()
    {
        // Для оффлайн-режима
        if (!NetworkClient.active)
        {
            InitializeLocal();
        }
    }
    
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        InitializeLocal();
    }
    
    private void InitializeLocal()
    {
        if (_isInitialized) return;
        
        // Подписываемся на событие использования очков ночного видения
        NightVisionItem.OnNightVisionUsed += OnNightVisionUsedFromInventory;
        
        _isInitialized = true;
        Debug.Log("[NightVisionController] Initialized and listening for night vision use");
    }
    
    /// <summary>
    /// Вызывается когда NightVisionItem используется через голосовую команду.
    /// </summary>
    private void OnNightVisionUsedFromInventory(int remainingCount)
    {
        // Проверяем кулдаун
        if (Time.time - _lastUseTime < _cooldown)
        {
            Debug.Log($"[NightVisionController] Cooldown: {_cooldown - (Time.time - _lastUseTime):F1}s");
            return;
        }
        
        // Если уже активно, сбрасываем и перезапускаем
        if (_isActive && _activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            DestroyCurrentLight();
        }
        
        ActivateNightVision();
        _lastUseTime = Time.time;
        
        Debug.Log($"[NightVisionController] Night Vision activated! Duration: {_activeDuration}s, Remaining in inventory: {remainingCount}");
    }
    
    /// <summary>
    /// Активирует ночное видение.
    /// </summary>
    private void ActivateNightVision()
    {
        CreateLight();
        _activeCoroutine = StartCoroutine(NightVisionRoutine());
    }
    
    /// <summary>
    /// Создаёт источник света.
    /// </summary>
    private void CreateLight()
    {
        if (_lightPrefab != null)
        {
            _currentLight = Instantiate(_lightPrefab, transform);
            _lightComponent = _currentLight.GetComponent<Light>();
        }
        else
        {
            // Создаём точечный свет если префаб не задан
            _currentLight = new GameObject("NightVisionLight");
            _currentLight.transform.SetParent(transform);
            _currentLight.transform.localPosition = Vector3.zero;
            
            _lightComponent = _currentLight.AddComponent<Light>();
            _lightComponent.type = LightType.Point;
        }
        
        if (_lightComponent != null)
        {
            _lightComponent.intensity = _lightIntensity;
            _lightComponent.range = _lightRange;
            _lightComponent.color = _lightColor;
        }
        
        _isActive = true;
    }
    
    /// <summary>
    /// Корутина работы ночного видения: активное время + затухание.
    /// </summary>
    private IEnumerator NightVisionRoutine()
    {
        // Ждём активное время
        yield return new WaitForSeconds(_activeDuration);
        
        // Плавное затухание (аналогично LightDestroy)
        if (_lightComponent != null)
        {
            yield return StartCoroutine(FadeOutLight());
        }
        
        DestroyCurrentLight();
    }
    
    /// <summary>
    /// Плавное затухание света (механизм из LightDestroy).
    /// </summary>
    private IEnumerator FadeOutLight()
    {
        if (_lightComponent == null) yield break;
        
        float startIntensity = _lightComponent.intensity;
        float elapsed = 0f;
        
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _fadeDuration;
            _lightComponent.intensity = Mathf.Lerp(startIntensity, 0f, t);
            yield return null;
        }
        
        _lightComponent.intensity = 0f;
    }
    
    /// <summary>
    /// Уничтожает текущий источник света.
    /// </summary>
    private void DestroyCurrentLight()
    {
        if (_currentLight != null)
        {
            Destroy(_currentLight);
            _currentLight = null;
            _lightComponent = null;
        }
        
        _isActive = false;
        _activeCoroutine = null;
    }
    
    private void OnDisable()
    {
        Cleanup();
    }
    
    private void OnDestroy()
    {
        Cleanup();
    }
    
    private void Cleanup()
    {
        if (_isInitialized)
        {
            NightVisionItem.OnNightVisionUsed -= OnNightVisionUsedFromInventory;
            _isInitialized = false;
        }
        
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
        }
        
        DestroyCurrentLight();
    }
}

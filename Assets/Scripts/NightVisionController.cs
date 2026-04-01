using System.Collections;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// Контроллер очков ночного видения.
/// Слушает событие NightVisionItem.OnNightVisionUsed и включает свет на заданное время.
/// </summary>
public class NightVisionController : MonoBehaviourPun
{
    [Header("Настройки света")]
    [SerializeField] private GameObject _lightPrefab;
    [SerializeField] private float _lightIntensity = 2f;
    [SerializeField] private float _lightRange = 50f;
    [SerializeField] private Color _lightColor = new Color(0.2f, 1f, 0.3f);
    
    [Header("Настройки таймера")]
    [SerializeField] private float _activeDuration = 10f;
    [SerializeField] private float _fadeDuration = 3f;
    
    [Header("Кулдаун")]
    [SerializeField] private float _cooldown = 1f;
    
    private GameObject _currentLight;
    private Light _lightComponent;
    private float _lastUseTime = -999f;
    private bool _isInitialized = false;
    private bool _isActive = false;
    private Coroutine _activeCoroutine;
    
    private void Start()
    {
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            InitializeLocal();
        }
    }
    
    private void InitializeLocal()
    {
        if (_isInitialized) return;
        
        NightVisionItem.OnNightVisionUsed += OnNightVisionUsedFromInventory;
        
        _isInitialized = true;
        Debug.Log("[NightVisionController] Initialized and listening for night vision use");
    }
    
    private void OnNightVisionUsedFromInventory(int remainingCount)
    {
        if (Time.time - _lastUseTime < _cooldown)
        {
            Debug.Log($"[NightVisionController] Cooldown: {_cooldown - (Time.time - _lastUseTime):F1}s");
            return;
        }
        
        if (_isActive && _activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            DestroyCurrentLight();
        }
        
        ActivateNightVision();
        _lastUseTime = Time.time;
        
        Debug.Log($"[NightVisionController] Night Vision activated! Duration: {_activeDuration}s, Remaining in inventory: {remainingCount}");
    }
    
    private void ActivateNightVision()
    {
        CreateLight();
        _activeCoroutine = StartCoroutine(NightVisionRoutine());
    }
    
    private void CreateLight()
    {
        if (_lightPrefab != null)
        {
            _currentLight = Instantiate(_lightPrefab, transform);
            _lightComponent = _currentLight.GetComponent<Light>();
        }
        else
        {
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
    
    private IEnumerator NightVisionRoutine()
    {
        yield return new WaitForSeconds(_activeDuration);
        
        if (_lightComponent != null)
        {
            yield return StartCoroutine(FadeOutLight());
        }
        
        DestroyCurrentLight();
    }
    
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

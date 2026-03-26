using System;
using UnityEngine;
using Mirror;

/// <summary>
/// Central manager for the echo system (Approach A: Point Light) with Mirror networking support.
/// MonoBehaviour singleton — lives on a scene object.
/// Each echo creates and animates Point Lights locally.
/// Networked echoes are synchronized across all clients.
/// </summary>
public class EchoManager : MonoBehaviour
{
    public static EchoManager Instance { get; private set; }

    private const int MaxEchoSources = 16;
    private const int MaxAmbientSources = 8;
    private const int MaxTotalSources = MaxEchoSources + MaxAmbientSources;

    [Header("Debug")]
    [SerializeField] private bool _showGizmos;

    private readonly EchoInstance[] _instances = new EchoInstance[MaxEchoSources];
    private readonly EchoInstance[] _ambientInstances = new EchoInstance[MaxAmbientSources];
    private int _activeCount;
    private int _ambientActiveCount;

    private static readonly int EchoCountId = Shader.PropertyToID("_EchoCount");
    private static readonly int EchoPositionsId = Shader.PropertyToID("_EchoPositions");
    private static readonly int EchoRadiiId = Shader.PropertyToID("_EchoRadii");
    private static readonly int EchoColorsId = Shader.PropertyToID("_EchoColors");

    private readonly Vector4[] _shaderPositions = new Vector4[MaxTotalSources];
    private readonly float[] _shaderRadii = new float[MaxTotalSources];
    private readonly Vector4[] _shaderColors = new Vector4[MaxTotalSources];

    /// <summary>
    /// Fired when an echo event is spawned. EnemyAI can subscribe to this.
    /// Args: position, intensity.
    /// </summary>
    public event Action<Vector3, float> OnEchoSpawned;

    private struct EchoInstance
    {
        public bool Active;
        public Vector3 Position;
        public float StartTime;
        public float Speed;
        public float MaxRadius;
        public float Intensity;
        public Color Color;
        public float Lifetime;
        public Light PointLight;
        public EchoType EchoType;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // Subscribe to network echo events
        EchoNetworkHelper.OnNetworkEchoSpawn += OnNetworkEchoReceived;
    }

    private void OnDisable()
    {
        // Unsubscribe from network echo events
        EchoNetworkHelper.OnNetworkEchoSpawn -= OnNetworkEchoReceived;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Called when echo is received from network.
    /// </summary>
    private void OnNetworkEchoReceived(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime, EchoType echoType)
    {
        SpawnEchoLocal(position, speed, maxRadius, intensity, color, lifetime, echoType);
    }

    /// <summary>
    /// Spawn echo using preset parameters. Networked version.
    /// </summary>
    public void SpawnEcho(Vector3 position, EchoPreset preset)
    {
        if (preset == null) return;
        SpawnEcho(position, preset.Speed, preset.MaxRadius, preset.Intensity, preset.Color, preset.Lifetime, preset.EchoType);
    }

    /// <summary>
    /// Spawn echo with explicit parameters. Networked version.
    /// </summary>
    public void SpawnEcho(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime, EchoType echoType = EchoType.Default)
    {
        // Если мы в сети
        if (NetworkClient.active)
        {
            // Находим EchoNetworkHelper для отправки команд
            var helper = EchoNetworkHelper.Instance;
            if (helper != null)
            {
                helper.RequestSpawnEcho(position, speed, maxRadius, intensity, color, lifetime, echoType);
            }
            else
            {
                // Если helper не найден, спавним локально
                SpawnEchoLocal(position, speed, maxRadius, intensity, color, lifetime, echoType);
            }
        }
        else
        {
            // Синглплеер - просто спавним локально
            SpawnEchoLocal(position, speed, maxRadius, intensity, color, lifetime, echoType);
        }
    }

    private void SpawnAmbientEchoLocal(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime, EchoType echoType = EchoType.Default)
    {
        int slot = FindFreeAmbientSlot();
        if (slot < 0) return;

        var go = new GameObject("AmbientEchoLight");
        go.transform.position = position;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = 0.1f;
        light.shadows = LightShadows.None;

        // Add small static trigger collider
        var echoCollider = go.AddComponent<EchoCollider>();
        echoCollider.Initialize(echoType);

        _ambientInstances[slot] = new EchoInstance
        {
            Active = true,
            Position = position,
            StartTime = Time.time,
            Speed = speed,
            MaxRadius = maxRadius,
            Intensity = intensity,
            Color = color,
            Lifetime = lifetime,
            PointLight = light,
            EchoType = echoType
        };
        _ambientActiveCount++;
    }

    /// <summary>
    /// Spawn a local-only ambient echo.
    /// Used by EchoSource for environmental sounds (dripping water, vents, etc.).
    /// </summary>
    public void SpawnAmbientEcho(Vector3 position, EchoPreset preset)
    {
        if (preset == null) return;
        SpawnAmbientEchoLocal(position, preset.Speed, preset.MaxRadius, preset.Intensity, preset.Color, preset.Lifetime, preset.EchoType);
    }

    /// <summary>
    /// Creates the Point Light locally.
    /// </summary>
    private void SpawnEchoLocal(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime, EchoType echoType = EchoType.Default)
    {
        int slot = FindFreeSlot();
        if (slot < 0) return;

        // Notify listeners (enemy AI)
        OnEchoSpawned?.Invoke(position, intensity);

        var go = new GameObject("EchoLight");
        go.transform.position = position;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = 0.1f;
        light.shadows = LightShadows.None;

        // Add small static trigger collider
        var echoCollider = go.AddComponent<EchoCollider>();
        echoCollider.Initialize(echoType);

        _instances[slot] = new EchoInstance
        {
            Active = true,
            Position = position,
            StartTime = Time.time,
            Speed = speed,
            MaxRadius = maxRadius,
            Intensity = intensity,
            Color = color,
            Lifetime = lifetime,
            PointLight = light,
            EchoType = echoType
        };
        _activeCount++;
    }

    private void Update()
    {
        float time = Time.time;

        UpdatePool(_instances, MaxEchoSources, ref _activeCount, time);
        UpdatePool(_ambientInstances, MaxAmbientSources, ref _ambientActiveCount, time);

        UploadShaderData(time);
    }

    private static void UpdatePool(EchoInstance[] pool, int size, ref int activeCount, float time)
    {
        for (int i = 0; i < size; i++)
        {
            ref var inst = ref pool[i];
            if (!inst.Active) continue;

            float elapsed = time - inst.StartTime;

            if (elapsed >= inst.Lifetime)
            {
                DestroyInstance(ref inst);
                activeCount--;
                continue;
            }

            float t = elapsed / inst.Lifetime;
            float currentRadius = Mathf.Min(inst.Speed * elapsed, inst.MaxRadius);

            float fade = 1f - t;
            fade *= fade;

            if (inst.PointLight != null)
            {
                inst.PointLight.range = currentRadius;
                inst.PointLight.intensity = inst.Intensity * fade;
            }
        }
    }

    /// <summary>
    /// Push active echo pulse data to global shader properties for edge detection.
    /// </summary>
    private void UploadShaderData(float time)
    {
        int count = 0;

        // Networked echoes
        count = CollectShaderData(_instances, MaxEchoSources, time, count);
        // Ambient echoes
        count = CollectShaderData(_ambientInstances, MaxAmbientSources, time, count);

        // Zero out unused slots
        for (int i = count; i < MaxTotalSources; i++)
        {
            _shaderPositions[i] = Vector4.zero;
            _shaderRadii[i] = 0f;
            _shaderColors[i] = Vector4.zero;
        }

        Shader.SetGlobalInt(EchoCountId, count);
        Shader.SetGlobalVectorArray(EchoPositionsId, _shaderPositions);
        Shader.SetGlobalFloatArray(EchoRadiiId, _shaderRadii);
        Shader.SetGlobalVectorArray(EchoColorsId, _shaderColors);
    }

    private int CollectShaderData(EchoInstance[] pool, int size, float time, int startIndex)
    {
        int count = startIndex;
        for (int i = 0; i < size && count < MaxTotalSources; i++)
        {
            ref var inst = ref pool[i];
            if (!inst.Active) continue;

            float elapsed = time - inst.StartTime;
            float currentRadius = Mathf.Min(inst.Speed * elapsed, inst.MaxRadius);
            float t = elapsed / inst.Lifetime;
            float fade = 1f - t;
            fade *= fade;

            _shaderPositions[count] = new Vector4(inst.Position.x, inst.Position.y, inst.Position.z, 0f);
            _shaderRadii[count] = currentRadius;
            _shaderColors[count] = new Vector4(inst.Color.r, inst.Color.g, inst.Color.b, fade * inst.Intensity);

            count++;
        }
        return count;
    }

    private int FindFreeSlot()
    {
        for (int i = 0; i < MaxEchoSources; i++)
        {
            if (!_instances[i].Active)
                return i;
        }

        // All slots full — evict the oldest
        float oldestTime = float.MaxValue;
        int oldestIndex = 0;
        for (int i = 0; i < MaxEchoSources; i++)
        {
            if (_instances[i].StartTime < oldestTime)
            {
                oldestTime = _instances[i].StartTime;
                oldestIndex = i;
            }
        }

        DestroyInstance(ref _instances[oldestIndex]);
        _activeCount--;
        return oldestIndex;
    }

    private int FindFreeAmbientSlot()
    {
        for (int i = 0; i < MaxAmbientSources; i++)
        {
            if (!_ambientInstances[i].Active)
                return i;
        }

        float oldestTime = float.MaxValue;
        int oldestIndex = 0;
        for (int i = 0; i < MaxAmbientSources; i++)
        {
            if (_ambientInstances[i].StartTime < oldestTime)
            {
                oldestTime = _ambientInstances[i].StartTime;
                oldestIndex = i;
            }
        }

        DestroyInstance(ref _ambientInstances[oldestIndex]);
        _ambientActiveCount--;
        return oldestIndex;
    }

    private static void DestroyInstance(ref EchoInstance inst)
    {
        if (inst.PointLight != null)
            Destroy(inst.PointLight.gameObject);

        inst = default;
    }

    private void OnDrawGizmos()
    {
        if (!_showGizmos || !Application.isPlaying) return;

        DrawPoolGizmos(_instances, MaxEchoSources);
        DrawPoolGizmos(_ambientInstances, MaxAmbientSources);
    }

    private static void DrawPoolGizmos(EchoInstance[] pool, int size)
    {
        for (int i = 0; i < size; i++)
        {
            ref var inst = ref pool[i];
            if (!inst.Active) continue;

            float elapsed = Time.time - inst.StartTime;
            float radius = Mathf.Min(inst.Speed * elapsed, inst.MaxRadius);

            Gizmos.color = new Color(inst.Color.r, inst.Color.g, inst.Color.b, 0.3f);
            Gizmos.DrawWireSphere(inst.Position, radius);
        }
    }
}

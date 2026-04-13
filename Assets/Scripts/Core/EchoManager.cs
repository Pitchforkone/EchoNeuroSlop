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
        public Color Color;
        public AnimationCurve IntensityCurve;
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
    private void OnNetworkEchoReceived(Vector3 position, float speed, float maxRadius, float peakIntensity, Color color, float lifetime, EchoType echoType)
    {
        var curve = CreateDefaultCurve(peakIntensity, lifetime);
        SpawnEchoLocal(position, speed, maxRadius, color, curve, lifetime, echoType);
    }

    /// <summary>
    /// Spawn echo using preset parameters. Networked version.
    /// </summary>
    public void SpawnEcho(Vector3 position, EchoPreset preset)
    {
        if (preset == null) return;
        SpawnEcho(position, preset.Speed, preset.MaxRadius, preset.PeakIntensity, preset.Color, preset.Lifetime, preset.EchoType, preset.IntensityCurve);
    }

    /// <summary>
    /// Spawn echo with preset + scaling (for "final echo" etc.).
    /// </summary>
    public void SpawnEcho(Vector3 position, EchoPreset preset, float radiusScale, float intensityScale, float timeScale)
    {
        if (preset == null) return;
        var scaledCurve = ScaleCurve(preset.IntensityCurve, intensityScale, timeScale);
        float lifetime = preset.Lifetime * timeScale;
        SpawnEcho(position, preset.Speed, preset.MaxRadius * radiusScale, preset.PeakIntensity * intensityScale, preset.Color, lifetime, preset.EchoType, scaledCurve);
    }

    /// <summary>
    /// Spawn echo with explicit parameters. Networked version.
    /// </summary>
    public void SpawnEcho(Vector3 position, float speed, float maxRadius, float peakIntensity, Color color, float lifetime, EchoType echoType = EchoType.Default, AnimationCurve intensityCurve = null)
    {
        // Если мы в сети
        if (NetworkClient.active)
        {
            // Находим EchoNetworkHelper для отправки команд
            var helper = EchoNetworkHelper.Instance;
            if (helper != null)
            {
                helper.RequestSpawnEcho(position, speed, maxRadius, peakIntensity, color, lifetime, echoType);
            }
            else
            {
                // Если helper не найден, спавним локально
                var curve = intensityCurve ?? CreateDefaultCurve(peakIntensity, lifetime);
                SpawnEchoLocal(position, speed, maxRadius, color, curve, lifetime, echoType);
            }
        }
        else
        {
            // Синглплеер — используем кривую напрямую
            var curve = intensityCurve ?? CreateDefaultCurve(peakIntensity, lifetime);
            SpawnEchoLocal(position, speed, maxRadius, color, curve, lifetime, echoType);
        }
    }

    private void SpawnAmbientEchoLocal(Vector3 position, float speed, float maxRadius, Color color, AnimationCurve intensityCurve, float lifetime, EchoType echoType = EchoType.Default)
    {
        int slot = FindFreeAmbientSlot();
        if (slot < 0) return;

        var go = new GameObject("AmbientEchoLight");
        go.transform.position = position;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 0f;
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
            Color = color,
            IntensityCurve = intensityCurve,
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
        SpawnAmbientEchoLocal(position, preset.Speed, preset.MaxRadius, preset.Color, preset.IntensityCurve, preset.Lifetime, preset.EchoType);
    }

    /// <summary>
    /// Creates the Point Light locally.
    /// </summary>
    private void SpawnEchoLocal(Vector3 position, float speed, float maxRadius, Color color, AnimationCurve intensityCurve, float lifetime, EchoType echoType = EchoType.Default)
    {
        int slot = FindFreeSlot();
        if (slot < 0) return;

        // Notify listeners (enemy AI) — pass peak intensity from curve
        float peak = 0f;
        if (intensityCurve != null)
            foreach (var key in intensityCurve.keys)
                if (key.value > peak) peak = key.value;
        OnEchoSpawned?.Invoke(position, peak);

        var go = new GameObject("EchoLight");
        go.transform.position = position;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 0f;
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
            Color = color,
            IntensityCurve = intensityCurve,
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

            float currentRadius = Mathf.Min(inst.Speed * elapsed, inst.MaxRadius);
            float intensity = inst.IntensityCurve != null ? inst.IntensityCurve.Evaluate(elapsed) : 0f;

            if (inst.PointLight != null)
            {
                inst.PointLight.range = currentRadius;
                inst.PointLight.intensity = intensity;
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
            float intensity = inst.IntensityCurve != null ? inst.IntensityCurve.Evaluate(elapsed) : 0f;

            _shaderPositions[count] = new Vector4(inst.Position.x, inst.Position.y, inst.Position.z, 0f);
            _shaderRadii[count] = currentRadius;
            _shaderColors[count] = new Vector4(inst.Color.r, inst.Color.g, inst.Color.b, intensity);

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

    private static AnimationCurve CreateDefaultCurve(float peakIntensity, float lifetime)
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(lifetime * 0.05f, peakIntensity),
            new Keyframe(lifetime * 0.25f, peakIntensity),
            new Keyframe(lifetime, 0f)
        );
    }

    private static AnimationCurve ScaleCurve(AnimationCurve source, float intensityScale, float timeScale)
    {
        if (source == null) return null;
        var keys = source.keys;
        var newKeys = new Keyframe[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            newKeys[i] = new Keyframe(
                keys[i].time * timeScale,
                keys[i].value * intensityScale,
                keys[i].inTangent * (intensityScale / timeScale),
                keys[i].outTangent * (intensityScale / timeScale)
            );
        }
        return new AnimationCurve(newKeys);
    }
}

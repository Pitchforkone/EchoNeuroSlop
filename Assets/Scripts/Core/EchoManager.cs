using UnityEngine;

/// <summary>
/// Central manager for the echo system (Approach A: Point Light).
/// Maintains up to 16 active echo pulses, spawns/animates/destroys Point Lights.
/// </summary>
public class EchoManager : MonoBehaviour
{
    public static EchoManager Instance { get; private set; }

    private const int MaxEchoSources = 16;

    [Header("Debug")]
    [SerializeField] private bool _showGizmos;

    private readonly EchoInstance[] _instances = new EchoInstance[MaxEchoSources];
    private int _activeCount;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Spawn a new echo pulse at the given world position using preset parameters.
    /// </summary>
    public void SpawnEcho(Vector3 position, EchoPreset preset)
    {
        if (preset == null) return;
        SpawnEcho(position, preset.Speed, preset.MaxRadius, preset.Intensity, preset.Color, preset.Lifetime);
    }

    /// <summary>
    /// Spawn a new echo pulse with explicit parameters.
    /// </summary>
    public void SpawnEcho(Vector3 position, float speed, float maxRadius, float intensity, Color color, float lifetime)
    {
        int slot = FindFreeSlot();
        if (slot < 0) return;

        var go = new GameObject("EchoLight");
        go.transform.position = position;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = 0.1f;
        light.shadows = LightShadows.None;

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
            PointLight = light
        };
        _activeCount++;
    }

    private void Update()
    {
        float time = Time.time;

        for (int i = 0; i < MaxEchoSources; i++)
        {
            ref var inst = ref _instances[i];
            if (!inst.Active) continue;

            float elapsed = time - inst.StartTime;

            if (elapsed >= inst.Lifetime)
            {
                DestroyInstance(ref inst);
                _activeCount--;
                continue;
            }

            float t = elapsed / inst.Lifetime;
            float currentRadius = inst.Speed * elapsed;
            currentRadius = Mathf.Min(currentRadius, inst.MaxRadius);

            // Intensity fades out over lifetime
            float fade = 1f - t;
            fade *= fade; // quadratic falloff for nicer visual

            if (inst.PointLight != null)
            {
                inst.PointLight.range = currentRadius;
                inst.PointLight.intensity = inst.Intensity * fade;
            }
        }
    }

    private int FindFreeSlot()
    {
        // Find an empty slot
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

    private static void DestroyInstance(ref EchoInstance inst)
    {
        if (inst.PointLight != null)
            Destroy(inst.PointLight.gameObject);

        inst = default;
    }

    private void OnDrawGizmos()
    {
        if (!_showGizmos || !Application.isPlaying) return;

        for (int i = 0; i < MaxEchoSources; i++)
        {
            ref var inst = ref _instances[i];
            if (!inst.Active) continue;

            float elapsed = Time.time - inst.StartTime;
            float radius = Mathf.Min(inst.Speed * elapsed, inst.MaxRadius);

            Gizmos.color = new Color(inst.Color.r, inst.Color.g, inst.Color.b, 0.3f);
            Gizmos.DrawWireSphere(inst.Position, radius);
        }
    }
}

using UnityEngine;

/// <summary>
/// Configuration for echo locator presets.
/// Create via Assets ? Create ? Echo/EchoLocatorConfig.
/// </summary>
[CreateAssetMenu(fileName = "EchoLocatorConfig", menuName = "Echo/EchoLocatorConfig")]
public class EchoLocatorConfig : ScriptableObject
{
    [Header("Presets")]
    [Tooltip("Preset for active ping echo (LMB or loud sound)")]
    [SerializeField] private EchoPreset _activePingPreset;

    [Tooltip("Preset for normal footstep echo")]
    [SerializeField] private EchoPreset _footstepPreset;

    [Tooltip("Preset for sprint footstep echo")]
    [SerializeField] private EchoPreset _sprintFootstepPreset;

    [Header("Audio")]
    [Tooltip("Footstep sounds (played randomly)")]
    [SerializeField] private AudioClip[] _footstepSounds;

    public EchoPreset ActivePingPreset => _activePingPreset;
    public EchoPreset FootstepPreset => _footstepPreset;
    public EchoPreset SprintFootstepPreset => _sprintFootstepPreset;
    public AudioClip[] FootstepSounds => _footstepSounds;
}

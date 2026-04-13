using Mirror;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
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
    [SerializeField] public EchoPreset _activePingPreset;
    public List<EchoTypeStep> _echoTypeSteps;
    public EchoTypeStep GetPresetForStep(TypeOfStep typeOfStep, bool IsRunning, bool IsCrouching)
    {
        foreach (var echoTypeStep in _echoTypeSteps)
        {
            if (echoTypeStep.ValidationType(typeOfStep, IsRunning, IsCrouching))
            {
                return echoTypeStep;
            }
        }
        return _echoTypeSteps.FirstOrDefault(x => x.typeOfStep == typeOfStep);
    }
}
[System.Serializable]
public class EchoTypeStep
{
    public TypeOfStep typeOfStep;
    public EchoPreset echoPreset;
    public AudioClip[] FootstepSounds;
    public bool IsRunning;
    public bool IsCrouching;
    public bool ValidationType(TypeOfStep typeOfStep, bool IsRunning, bool IsCrouching)
    {
        return this.typeOfStep == typeOfStep && this.IsRunning == IsRunning && this.IsCrouching == IsCrouching;
    }
}
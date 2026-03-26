/// <summary>
/// Defines the type of echo pulse.
/// </summary>
public enum EchoType
{
    /// <summary>
    /// Default echo type.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Voice-generated echo.
    /// </summary>
    Voice = 1,

    /// <summary>
    /// Footstep echo.
    /// </summary>
    Step = 2,

    /// <summary>
    /// Non-triggering echo (ambient, environmental).
    /// </summary>
    NonTrigger = 3,

    /// <summary>
    /// Grenade or explosive echo.
    /// </summary>
    Grenade = 4
}

namespace BKKleaner.UI;

/// <summary>
/// Global, app-wide animation switch. Custom animated controls consult this so the
/// "enable animations" setting can disable transitions without restarting.
/// </summary>
public static class AnimationSettings
{
    public static bool Enabled { get; set; } = true;
}

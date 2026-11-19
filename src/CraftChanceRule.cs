namespace CraftCrit.Meitou;

/// <summary>
/// Mutually exclusive ways to adjust a new craft's critical chance.
/// </summary>
public enum CraftChanceMode
{
    /// <summary>
    /// Uses an absolute percentage.
    /// </summary>
    Fixed,

    /// <summary>
    /// Multiplies the game's normal skill-based chance.
    /// </summary>
    Multiplier,
}

/// <summary>
/// An immutable snapshot containing only the selected mode's value, sent to the simulation as one command.
/// </summary>
internal sealed record CraftChanceRule(bool Enabled, CraftChanceMode Mode, float Value)
{
    /// <summary>
    /// Calculates a probability in 0..1; a multiplier cannot raise a zero base chance.
    /// </summary>
    internal float Apply(float normal)
    {
        if (!Enabled) return normal;
        return Mode == CraftChanceMode.Fixed ? Value / 100f : Math.Clamp(normal * Value, 0, 1);
    }

    /// <summary>
    /// The displayed whole percentage, preserving fixed integers and the game's truncation for skill-based chances.
    /// </summary>
    internal int Percentage(float normal) => Enabled && Mode == CraftChanceMode.Fixed
        ? (int)Value
        : Math.Clamp((int)(Apply(normal) * 100f), 0, 100);
}

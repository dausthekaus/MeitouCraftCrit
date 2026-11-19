using Meitou.Modding;

namespace CraftCrit.Meitou;

/// <summary>
/// The native Mods page preferences; the game persists them in meitou.user.json.
/// </summary>
public sealed class CraftCritSettings
{
    /// <summary>
    /// Whether to replace the game's skill-based critical chance.
    /// </summary>
    int _overrideEnabled = 1;

    /// <summary>
    /// The configured percentage, shared safely with the world factory.
    /// </summary>
    int _chancePercent = 100;

    /// <summary>
    /// The selected adjustment, shared safely with the world factory.
    /// </summary>
    int _mode;

    /// <summary>
    /// The factor applied only in multiplier mode.
    /// </summary>
    float _multiplier = 2;

    /// <summary>
    /// Turning the override off restores the original skill-based calculation for future rolls.
    /// </summary>
    [Setting("Override critical craft chance", Hint = "Off uses the normal skill-based chance. On uses the selected mode.")]
    public bool OverrideEnabled
    {
        get => Volatile.Read(ref _overrideEnabled) != 0;
        set => Volatile.Write(ref _overrideEnabled, value ? 1 : 0);
    }

    /// <summary>
    /// Selects either an absolute chance or a multiplier; the two values are never combined.
    /// </summary>
    [Setting("Chance mode", Hint = "Fixed replaces the chance. Multiplier scales the normal skill-based chance.")]
    public CraftChanceMode Mode
    {
        get => (CraftChanceMode)Volatile.Read(ref _mode);
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref _mode, (int)value);
        }
    }

    /// <summary>
    /// An absolute chance for each new critical roll, from never to always.
    /// </summary>
    [Setting("Critical craft chance (%)", Min = 0, Max = 100, Step = 1, VisibleWhen = nameof(ShowFixedChance),
        Hint = "0% never crits; 100% always crits. Applies when a craft first rolls; existing rolls are kept.")]
    public int ChancePercent
    {
        get => Volatile.Read(ref _chancePercent);
        set => Volatile.Write(ref _chancePercent, Math.Clamp(value, 0, 100));
    }

    /// <summary>
    /// A factor for the normal chance; 1 is unchanged, 2 doubles it, and the result is capped at 100%.
    /// </summary>
    [Setting("Critical craft multiplier", Min = 0, Max = 10, Step = 0.1, VisibleWhen = nameof(ShowMultiplier),
        Hint = "Normal chance × factor, capped at 100%. 1× is normal; 0× disables crits; a 0% base stays 0%.")]
    public float Multiplier
    {
        get => Volatile.Read(ref _multiplier);
        set
        {
            if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref _multiplier, Math.Clamp(value, 0, 10));
        }
    }

    /// <summary>
    /// Shows the fixed slider only when that mode is active.
    /// </summary>
    public bool ShowFixedChance => OverrideEnabled && Mode == CraftChanceMode.Fixed;

    /// <summary>
    /// Shows the multiplier slider only when that mode is active.
    /// </summary>
    public bool ShowMultiplier => OverrideEnabled && Mode == CraftChanceMode.Multiplier;

    /// <summary>
    /// Captures only the active mode and value, so changing an inactive slider cannot affect the simulation.
    /// </summary>
    internal CraftChanceRule SelectedRule
    {
        get
        {
            if (!OverrideEnabled) return new(false, CraftChanceMode.Fixed, 0);
            var mode = Mode;
            return new(true, mode, mode == CraftChanceMode.Fixed ? ChancePercent : Multiplier);
        }
    }
}

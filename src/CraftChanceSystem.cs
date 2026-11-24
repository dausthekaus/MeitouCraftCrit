using Meitou.Simulation;

namespace CraftCrit.Meitou;

/// <summary>
/// A settings change applied in the world's serial input phase.
/// </summary>
internal sealed record SetCraftChance(CraftChanceRule Rule) : SimCommand;

/// <summary>
/// Per-world chance state. The original production system still owns every roll, its counter, and saved craft outcomes.
/// </summary>
internal sealed class CraftChanceSystem(CraftChanceRule rule, Action? invalidatePreview = null) : ITickSystem, IStateHashed
{
    /// <summary>
    /// The active rule. Only Inputs replaces this immutable snapshot after construction.
    /// </summary>
    CraftChanceRule _rule = rule;

    /// <summary>
    /// The chance used by crafting and the published Craft tab predictions.
    /// </summary>
    internal CraftChanceRule Rule => Volatile.Read(ref _rule);

    /// <summary>
    /// Changes future rolls and invalidates the game's cached crafting predictions at a simulation boundary.
    /// </summary>
    public void Inputs(World world, IReadOnlyList<SimCommand> commands)
    {
        foreach (var command in commands)
        {
            if (command is not SetCraftChance change) continue;
            if (change.Rule == _rule) continue;

            Volatile.Write(ref _rule, change.Rule);
            invalidatePreview?.Invoke();
            world.Machines.Touch();
        }
    }

    /// <summary>
    /// Makes the active configuration part of the world's determinism hash.
    /// </summary>
    public void Hash(ref StateHasher hasher)
    {
        var rule = Rule;
        hasher.Add(rule.Enabled);
        hasher.Add((int)rule.Mode);
        hasher.Add(rule.Value);
    }
}

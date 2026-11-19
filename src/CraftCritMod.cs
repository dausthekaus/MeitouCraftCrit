using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Production;

namespace CraftCrit.Meitou;

/// <summary>
/// Entry point for the independent CraftCrit-Meitou code mod.
/// </summary>
public sealed class CraftCritMod : IMod
{
    /// <summary>
    /// Preferences persisted by MeitouClient and read when each world's system is created.
    /// </summary>
    readonly CraftCritSettings _settings = new();

    /// <summary>
    /// Sends changes from the settings page to the running world's command queue.
    /// </summary>
    ModContext? _context;

    /// <summary>
    /// Called by MeitouClient after game data loads and before the first world is created.
    /// </summary>
    public void Initialize(ModContext context)
    {
        _context = context;
        context.AddSettings(_settings).Changed += SettingsChanged;
        context.AddSystem("critical-craft-chance", CreateSystem, SystemAnchor.Before(StandardParts.Production));
        CraftingPatches.Install(context.Harmony);
        if (CraftQualityCompatibility.Install(context.Harmony))
            context.Log("Applied legacy crafting grade fix: bench tech level no longer caps finished item quality.");
        context.Log("CraftCrit-Meitou initialized. Configure critical craft chance in Settings > Mods.");
    }

    /// <summary>
    /// Gives each production system its own chance state, including new games and loaded saves.
    /// </summary>
    ITickSystem CreateSystem(SystemBuildContext context)
    {
        var production = context.Find<ProductionSystem>();
        return production is null
            ? new CraftChanceSystem(_settings.SelectedRule)
            : CraftingPatches.Bind(production, _settings.SelectedRule);
    }

    /// <summary>
    /// Applies interface changes through a simulation command; before a world exists the next factory reads the saved preference.
    /// </summary>
    void SettingsChanged(ModSetting setting)
    {
        var context = _context!;
        context.Enqueue(new SetCraftChance(_settings.SelectedRule) { Tick = context.Tick });
    }
}

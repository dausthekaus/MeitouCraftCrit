using System.Collections;
using System.Reflection;
using HarmonyLib;
using Meitou.Data.Gameplay.Construction;
using Meitou.Simulation;
using Meitou.Simulation.Production;

namespace CraftCrit.Meitou;

/// <summary>
/// Resolves the legacy preview method or the nightly's extracted cache without referencing newer engine types in the DLL.
/// </summary>
internal sealed class CraftPreviewTarget(MethodInfo method, FieldInfo? cache = null, FieldInfo? entries = null, FieldInfo? published = null)
{
    /// <summary>
    /// The method containing the native chance-to-percentage calculation.
    /// </summary>
    internal MethodInfo Method { get; } = method;

    /// <summary>
    /// Selects only the two layouts inspected in official source; unknown layouts refuse initialization.
    /// </summary>
    internal static CraftPreviewTarget Resolve(MethodInfo chance)
    {
        var legacy = AccessTools.DeclaredMethod(typeof(ProductionSystem), nameof(ProductionSystem.SmithsView), [typeof(World)]);
        if (legacy is not null && PatchProcessor.GetOriginalInstructions(legacy).Any(c => c.Calls(chance))) return new(legacy);

        var type = typeof(ProductionSystem).Assembly.GetType("Meitou.Simulation.Production.SmithViewCache");
        if (type is null) throw Unsupported();

        var predictions = AccessTools.DeclaredMethod(type, "Predictions", [typeof(Machine), typeof(CraftBench), typeof(CraftState), typeof(float)]);
        var cache = AccessTools.DeclaredField(typeof(ProductionSystem), "_smiths");
        var entries = AccessTools.DeclaredField(type, "_entries");
        var published = AccessTools.DeclaredField(type, "_published");
        if (predictions is null || predictions.IsStatic || cache is null || cache.IsStatic || cache.FieldType != type
            || entries is null || entries.IsStatic || !typeof(IDictionary).IsAssignableFrom(entries.FieldType)
            || published is null || published.IsStatic || published.IsInitOnly
            || published.FieldType != typeof(IReadOnlyDictionary<string, CraftSmithView>)) throw Unsupported();
        if (!PatchProcessor.GetOriginalInstructions(predictions).Any(c => c.Calls(chance))) throw Unsupported();

        return new(predictions, cache, entries, published);
    }

    /// <summary>
    /// The instance loaded by ldarg.0 at this layout's chance call; its settings belong to this production system's world.
    /// </summary>
    internal object OwnerOf(ProductionSystem production) => cache is null ? production
        : cache.GetValue(production) ?? throw Unsupported();

    /// <summary>
    /// Drops the nightly's derived views after a serial settings change. Machines.Touch alone leaves its per-bench entries reusable.
    /// </summary>
    internal void Invalidate(object owner)
    {
        if (cache is null) return;
        if (entries!.GetValue(owner) is not IDictionary views) throw Unsupported();

        views.Clear();
        published!.SetValue(owner, null);
    }

    /// <summary>
    /// A diagnostic for engine layouts that need inspection before they can be safely supported.
    /// </summary>
    static InvalidOperationException Unsupported() => new("Unsupported MeitouClient crafting preview: neither known preview layout was found.");
}

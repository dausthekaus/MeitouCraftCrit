using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Meitou.Data.Gameplay.Construction;
using Meitou.Simulation;
using Meitou.Simulation.Production;

namespace CraftCrit.Meitou;

/// <summary>
/// Overrides the roll's chance and the Craft tab predictions by patching large callers, avoiding an inlined helper patch.
/// </summary>
internal static class CraftingPatches
{
    /// <summary>
    /// Weak keys keep each world's production state separate without retaining old worlds.
    /// </summary>
    static readonly ConditionalWeakTable<ProductionSystem, CraftChanceSystem> Systems = new();

    /// <summary>
    /// The official chance helper, whose call sites are replaced without changing the deterministic roll.
    /// </summary>
    static readonly MethodInfo OriginalChance = typeof(CraftBench).GetMethod(nameof(CraftBench.CriticalChance),
        [typeof(float), typeof(int), typeof(int)])!;

    /// <summary>
    /// The replacement takes the production instance as an extra argument to find its world's chance state.
    /// </summary>
    static readonly MethodInfo ModChance = AccessTools.DeclaredMethod(typeof(CraftingPatches), nameof(CriticalChance));

    /// <summary>
    /// Computes the exact displayed percentage from the same raw skill-based probability used by crafting.
    /// </summary>
    static readonly MethodInfo ModPercentage = AccessTools.DeclaredMethod(typeof(CraftingPatches), nameof(CriticalPercentage));

    /// <summary>
    /// Installs both call-site patches through the game's shared Harmony instance; an unsupported method fails initialization.
    /// </summary>
    internal static void Install(Harmony harmony)
    {
        var craft = RequireMethod("CraftStep", [typeof(World), typeof(Machine), typeof(float), typeof(float), typeof(float)]);
        var preview = RequireMethod(nameof(ProductionSystem.SmithsView), [typeof(World)]);
        var transpiler = new HarmonyMethod(typeof(CraftingPatches), nameof(ReplaceChance));
        harmony.Patch(craft, transpiler: transpiler);
        harmony.Patch(preview, transpiler: transpiler);
    }

    /// <summary>
    /// Associates a newly built world's production system with its own simulation settings.
    /// </summary>
    internal static CraftChanceSystem Bind(ProductionSystem production, CraftChanceRule rule)
    {
        var system = new CraftChanceSystem(rule);
        Systems.Add(production, system);
        return system;
    }

    /// <summary>
    /// Uses the configured absolute probability, or the official skill-based calculation when the override is off or no system is bound.
    /// </summary>
    static float CriticalChance(float skill, int stat, int level, ProductionSystem production)
    {
        float normal = CraftBench.CriticalChance(skill, stat, level);
        return Systems.TryGetValue(production, out var system) ? system.Rule.Apply(normal) : normal;
    }

    /// <summary>
    /// Uses the unrounded normal probability for multiplier previews, while preserving exact integers in fixed mode.
    /// </summary>
    static int CriticalPercentage(float skill, int stat, int level, ProductionSystem production)
    {
        float normal = CraftBench.CriticalChance(skill, stat, level);
        return Systems.TryGetValue(production, out var system)
            ? system.Rule.Percentage(normal)
            : Math.Clamp((int)(normal * 100f), 0, 100);
    }

    /// <summary>
    /// Resolves the exact method signatures inspected in MeitouClient's source.
    /// </summary>
    static MethodInfo RequireMethod(string name, Type[] parameters) =>
        AccessTools.DeclaredMethod(typeof(ProductionSystem), name, parameters)
        ?? throw new MissingMethodException($"Unsupported MeitouClient crafting API: ProductionSystem.{name} was not found.");

    /// <summary>
    /// Adds the production instance to each chance call, preserving branch labels; refuses a changed or conflicting method with no such call.
    /// </summary>
    static IEnumerable<CodeInstruction> ReplaceChance(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = instructions.ToList();
        bool preview = original.Name == nameof(ProductionSystem.SmithsView);
        int replaced = 0;
        for (int i = 0; i < code.Count; i++)
        {
            var instruction = code[i];
            if (!instruction.Calls(OriginalChance))
            {
                yield return instruction;
                continue;
            }

            if (preview)
            {
                // The source converts chance * 100 to int. Replace that conversion along with the chance call so fixed percentages stay exact.
                if (i + 3 >= code.Count || code[i + 1].opcode != OpCodes.Ldc_R4 || !Equals(code[i + 1].operand, 100f)
                    || code[i + 2].opcode != OpCodes.Mul || code[i + 3].opcode != OpCodes.Conv_I4
                    || code.Skip(i + 1).Take(3).Any(next => next.labels.Count != 0 || next.blocks.Count != 0))
                    throw new InvalidOperationException("Unsupported MeitouClient crafting preview: the chance-to-percentage conversion changed.");

                i += 3;
            }

            // Keep the call's labels and exception markers on the first replacement instruction.
            instruction.opcode = OpCodes.Ldarg_0;
            instruction.operand = null;
            yield return instruction;
            yield return new CodeInstruction(OpCodes.Call, preview ? ModPercentage : ModChance);
            replaced++;
        }

        if (replaced == 0)
            throw new InvalidOperationException($"Unsupported or conflicting MeitouClient crafting code: no CriticalChance call in {original.Name}.");
    }
}

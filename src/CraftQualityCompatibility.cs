using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Meitou.Data.Gameplay.Construction;
using Meitou.Simulation.Production;

namespace CraftCrit.Meitou;

/// <summary>
/// Matches the current official crafting rules on older clients that incorrectly cap item grade at the bench's tech level.
/// </summary>
internal static class CraftQualityCompatibility
{
    /// <summary>
    /// The grade calculation immediately preceding each obsolete cap in the 0.22.1 client.
    /// </summary>
    static readonly MethodInfo ArmourQuality = AccessTools.DeclaredMethod(typeof(CraftBench), nameof(CraftBench.ArmourQuality));

    /// <summary>
    /// The obsolete cap's property read.
    /// </summary>
    static readonly MethodInfo TechLevel = AccessTools.PropertyGetter(typeof(CraftBench), nameof(CraftBench.TechLevel));

    /// <summary>
    /// The obsolete cap's integer minimum operation.
    /// </summary>
    static readonly MethodInfo Minimum = AccessTools.DeclaredMethod(typeof(Math), nameof(Math.Min), [typeof(int), typeof(int)]);

    /// <summary>
    /// Patches only the inspected two-cap implementation; clients already following current source need no patch.
    /// </summary>
    internal static bool Install(Harmony harmony)
    {
        var finish = AccessTools.DeclaredMethod(typeof(ProductionSystem), "Finish")
            ?? throw new MissingMethodException("Unsupported MeitouClient crafting API: ProductionSystem.Finish was not found.");
        var code = PatchProcessor.GetOriginalInstructions(finish);
        int caps = CountCaps(code);
        if (caps == 0) return false;
        if (caps != 2) throw new InvalidOperationException("Unsupported MeitouClient grade caps: expected both known cap sites.");

        harmony.Patch(finish, transpiler: new HarmonyMethod(typeof(CraftQualityCompatibility), nameof(RemoveCaps)));
        return true;
    }

    /// <summary>
    /// Recognizes only ArmourQuality followed by a local bench, its TechLevel getter and integer Min, without branch targets in the cap.
    /// </summary>
    static bool IsCap(IReadOnlyList<CodeInstruction> code, int i) => i + 3 < code.Count && code[i].Calls(ArmourQuality)
        && code[i + 1].IsLdloc() && code[i + 2].Calls(TechLevel) && code[i + 3].Calls(Minimum)
        && !code.Skip(i + 1).Take(3).Any(c => c.labels.Count != 0 || c.blocks.Count != 0);

    /// <summary>
    /// Counts the exact old implementation's cap sequences.
    /// </summary>
    static int CountCaps(IReadOnlyList<CodeInstruction> code) => Enumerable.Range(0, code.Count).Count(i => IsCap(code, i));

    /// <summary>
    /// Leaves the calculated grade on the stack in both the armour and crossbow/robotics branches, as current official source does.
    /// </summary>
    static IEnumerable<CodeInstruction> RemoveCaps(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        if (CountCaps(code) != 2) throw new InvalidOperationException("Conflicting MeitouClient grade patch: expected both known cap sites.");
        for (int i = 0; i < code.Count; i++)
        {
            yield return code[i];
            if (IsCap(code, i)) i += 3;
        }
    }
}

using Meitou.Modding;

namespace CraftCrit.Meitou;

/// <summary>
/// Entry point for the independent CraftCrit-Meitou code mod.
/// </summary>
public sealed class CraftCritMod : IMod
{
    /// <summary>
    /// Called by MeitouClient after game data loads and before the first world is created.
    /// </summary>
    public void Initialize(ModContext context)
    {
        context.Log("CraftCrit-Meitou initialized.");
    }
}

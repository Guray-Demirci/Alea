namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Vortex Checker: "Removes the checker limit of the point it stands on."
    /// Point capacity is no longer a fixed rule — it depends on the abilities of the checkers on the point.
    /// </summary>
    public interface IStackLimitOverride : ICheckerAbility
    {
        /// <summary>Receives the default limit and returns the effective one (<c>int.MaxValue</c> for unlimited).</summary>
        int GetStackLimit(int defaultLimit);
    }
}

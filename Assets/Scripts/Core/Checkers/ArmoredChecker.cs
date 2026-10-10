using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Armored Checker. "Can never be captured. Counts as a made point even when alone."
    /// Implements TWO separate abilities because these are two different rules:
    /// being uncapturable (<see cref="ICaptureImmunity"/>) and forming a point alone
    /// (<see cref="IBlockadeOverride"/>) are independent.
    /// Inside a Fog Tile GetAbility returns null and both drop automatically.
    /// </summary>
    public class ArmoredChecker : SpecialChecker, ICaptureImmunity, IBlockadeOverride
    {
        public const string Id = "checker_armored";

        // --- ICaptureImmunity ---
        public bool IsImmune => true;                 // Permanent armor, never runs out.
        public void OnImmunityTriggered() { }         // Nothing to spend.

        // --- IBlockadeOverride ---
        public bool FormsBlockadeAlone => true;

        public ArmoredChecker(int ownerId) : base(ownerId, Id, "Armored Checker") { }
    }
}

using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Armored Checker. "Can never be captured. Counts as a made point even when alone."
    /// Implements TWO separate abilities because these are two different rules:
    /// being uncapturable (<see cref="ICaptureImmunity"/>) and forming a point alone
    /// (<see cref="IBlockadeOverride"/>) are independent.
    /// Inside a Fog Tile GetAbility returns null and both drop automatically.
    /// </summary>
    public class ArmoredChecker : BaseChecker, ICaptureImmunity, IBlockadeOverride, IPermanent
    {
        public const string Id = "checker_armored";

        public override string TypeId => Id;

        // --- ICaptureImmunity ---
        public bool IsImmune => true;                 // Permanent armor, never runs out.
        public void OnImmunityTriggered() { }         // Nothing to spend.

        // --- IBlockadeOverride ---
        public bool FormsBlockadeAlone => true;

        // --- IPurchasable ---
        public string ItemId => Id;
        public string DisplayName => "Armored Checker";
        public int Price => 500;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public ArmoredChecker(int ownerId) : base(ownerId) { }
    }
}

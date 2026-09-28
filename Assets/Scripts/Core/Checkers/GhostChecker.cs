using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Ghost Checker. "Can jump over the opponent's points and ignores blockades."
    /// Difference from the Parasite: the Ghost ignores the blockade to enter a point and captures a
    /// single enemy checker normally; only on a made point (2+ checkers) does it land beside them without capturing.
    /// </summary>
    public class GhostChecker : BaseChecker, IBlockadeBypass, IPermanent, IStarterItem
    {
        public const string Id = "checker_ghost";

        public override string TypeId => Id;

        // --- IBlockadeBypass ---
        public bool CanBypassBlockade(Node target) => true;

        // --- IPurchasable ---
        public string ItemId => Id;
        public string DisplayName => "Ghost Checker";
        public int Price => 450;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        // --- IStarterItem ---
        public int StartingAmount => 1;
        public bool IsDefaultUnlocked => false;

        public GhostChecker(int ownerId) : base(ownerId) { }
    }
}

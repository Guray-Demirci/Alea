using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Doomsday Checker. "If it lands exactly on a point the opponent has made, it captures ALL
    /// enemy checkers on that point at once and kicks them all to the bar."
    /// The checker that inverts classic backgammon's most basic restriction (a made point cannot be entered).
    /// </summary>
    public class DoomsdayChecker : BaseChecker, IAreaCapture, IPermanent
    {
        public const string Id = "checker_doomsday";

        public override string TypeId => Id;

        /// <summary>Works only against a real made point (2+ enemy checkers); a single checker is captured normally.</summary>
        public bool CanCaptureEntirePoint(Node target) => target.CountEnemiesOf(OwnerId) >= 2;

        public string ItemId => Id;
        public string DisplayName => "Doomsday Checker";
        public int Price => 1000;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public DoomsdayChecker(int ownerId) : base(ownerId) { }
    }
}

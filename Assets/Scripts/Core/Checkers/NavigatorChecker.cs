using Tavla.Core.Abilities;
using Tavla.Core.Board;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Navigator Checker. "Leaves footprints behind as it advances. On every point it has stepped on, capturing a
    /// checker is worth 2 points and bearing one off 4." Both on the game's score scale
    /// (<see cref="GameState.ScoreScale"/>).
    ///
    /// It scores nothing itself: it only marks every point it lands on — each stop of a compound move too — and any
    /// of its owner's checkers then earns more there, the Navigator included: the footprint goes down as it lands,
    /// so the checker it captures on a brand-new point already counts. The footprints are board state
    /// (<see cref="ScoreMark"/>), so they stay for the rest of the match even if the Navigator is captured or borne off.
    /// </summary>
    public class NavigatorChecker : SpecialChecker, IPointMarker
    {
        public const string Id = "checker_navigator";

        /// <summary>The GDD's capture 2 / bear-off 4, on the game's score scale.</summary>
        private static readonly ScoreMark Footprint =
            new ScoreMark(2 * GameState.ScoreScale, 4 * GameState.ScoreScale);

        public ScoreMark LandingMark => Footprint;

        public NavigatorChecker(int ownerId) : base(ownerId, Id, "Navigator Checker") { }
    }
}

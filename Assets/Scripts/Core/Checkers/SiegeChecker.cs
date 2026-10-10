using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Siege Checker. "When another checker of your colour arrives on this checker's point and makes a POINT,
    /// you earn +2 points."
    /// This checker is the reason the isMover=false hook exists: the trigger is not its own move
    /// but the move of a friendly checker.
    /// </summary>
    public class SiegeChecker : SpecialChecker, IMoveReactor
    {
        public const string Id = "checker_siege";

        /// <summary>The GDD's "+2 points", on the game's score scale.</summary>
        private const int PointBonus = 2 * GameState.ScoreScale;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            // Must be a friendly checker's move, not our own.
            if (isMover || result.PlayerId != OwnerId)
                return;

            // Did THIS arrival make the point? (this checker + the arriving friendly one = exactly 2)
            // A third checker joins a point that is already made, so it earns nothing.
            Node node = context.Board.Nodes[result.ToIndex];
            if (node.CountFor(OwnerId) != 2)
                return;

            context.AddScore(OwnerId, PointBonus);
            context.Board.ReportAbility(AbilityIds.SiegeBonus, this, result.ToIndex);
        }

        public SiegeChecker(int ownerId) : base(ownerId, Id, "Siege Checker") { }
    }
}

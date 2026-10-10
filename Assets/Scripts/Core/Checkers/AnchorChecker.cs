using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Anchor Checker. "When moved, one of the two dice of your next roll is pinned to the number
    /// of points it moved. Removes luck entirely."
    /// The only checker that touches the dice system; the bridge is <see cref="IMatchContext.PinNextDie"/>.
    /// </summary>
    public class AnchorChecker : SpecialChecker, IMoveReactor
    {
        public const string Id = "checker_anchor";

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            // Measured by the rules engine from the real origin, so entering from the bar counts too.
            int distance = result.Distance;

            // A die cannot show more than 6 — e.g. a distance stretched by another ability is not pinned.
            if (distance < 1 || distance > GameState.MaxDieValue)
                return;

            context.PinNextDie(OwnerId, distance);
            context.Board.ReportAbility(AbilityIds.AnchorPin, this, result.ToIndex);
        }

        public AnchorChecker(int ownerId) : base(ownerId, Id, "Anchor Checker") { }
    }
}

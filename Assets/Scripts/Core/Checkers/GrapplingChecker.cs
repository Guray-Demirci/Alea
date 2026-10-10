using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Grappling Checker. "When it lands on a point and there is an open (single) enemy checker exactly one point
    /// ahead of or behind it, it hooks that checker onto its own point and captures it."
    /// One hook per landing; the point ahead is tried first.
    /// </summary>
    public class GrapplingChecker : SpecialChecker, IMoveReactor
    {
        public const string Id = "checker_grappling";

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            // A tile may already have flung or captured it; it hooks from where it really stands.
            if (context.Board.FindNodeIndexOf(this) != result.ToIndex)
                return;

            int ahead = GameState.GetDirection(OwnerId);
            if (!TryHook(context, result.ToIndex + ahead))
                TryHook(context, result.ToIndex - ahead);
        }

        private bool TryHook(IMatchContext context, int index)
        {
            if (!GameState.IsOnBoard(index))
                return false;

            // Only an open checker: a single enemy that the rules would let us capture.
            Node node = context.Board.Nodes[index];
            if (node.CountEnemiesOf(OwnerId) != 1 || node.IsBlockedFor(OwnerId))
                return false;

            context.Board.ReportAbility(AbilityIds.GrapplingHook, this, index);
            context.Board.ForceCapture(node.GetTopEnemyOf(OwnerId), index, OwnerId);
            return true;
        }

        public GrapplingChecker(int ownerId) : base(ownerId, Id, "Grappling Checker") { }
    }
}

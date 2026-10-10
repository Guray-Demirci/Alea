using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Clover Checker. "When you move it, it hits the checker on the point just ahead of or just behind the point
    /// it lands on with a 50% chance. If neither of those points holds a checker, it hits itself with a 15% chance."
    ///
    /// Only enemies are hit, so "no checker" means no enemy next to it. With enemies on both sides the one ahead
    /// is struck first, as with the Grappling Checker. The strike takes the topmost enemy of the point, open or
    /// not; one that cannot be captured (Armored) is passed over for the other side.
    /// </summary>
    public class CloverChecker : SpecialChecker, IMoveReactor
    {
        public const string Id = "checker_clover";

        private const float StrikeChance = 0.5f;
        private const float SelfHitChance = 0.15f;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            // It acts from the point it landed on: not once borne off, nor after a tile has flung or captured it.
            int point = result.ToIndex;
            if (!GameState.IsOnBoard(point) || context.Board.FindNodeIndexOf(this) != point)
                return;

            int ahead = point + GameState.GetDirection(OwnerId);
            int behind = point - GameState.GetDirection(OwnerId);

            if (HasEnemy(context, ahead) || HasEnemy(context, behind))
            {
                if (context.Random.Chance(StrikeChance) && !TryStrike(context, ahead))
                    TryStrike(context, behind);

                return;
            }

            if (!context.Random.Chance(SelfHitChance))
                return;

            // Hits itself. It goes through ForceCapture, so the Phoenix/Sacrificial rules also apply.
            context.Board.ReportAbility(AbilityIds.CloverSelfHit, this, point);
            context.Board.ForceCapture(this, point, OwnerId);
        }

        private bool HasEnemy(IMatchContext context, int index)
        {
            return GameState.IsOnBoard(index) && context.Board.Nodes[index].CountEnemiesOf(OwnerId) > 0;
        }

        private bool TryStrike(IMatchContext context, int index)
        {
            if (!GameState.IsOnBoard(index))
                return false;

            // Checkers with immunity (e.g. Armored) are not affected by this ability either.
            BaseChecker enemy = context.Board.Nodes[index].GetTopEnemyOf(OwnerId);
            if (enemy == null || !enemy.CanBeCaptured)
                return false;

            context.Board.ReportAbility(AbilityIds.CloverStrike, this, index);
            context.Board.ForceCapture(enemy, index, OwnerId);
            return true;
        }

        public CloverChecker(int ownerId) : base(ownerId, Id, "Clover Checker") { }
    }
}

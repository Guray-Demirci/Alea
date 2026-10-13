using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Bowman Checker. "Every turn, with the first die it is played with, it picks as many checkers as the points it
    /// moved and rains arrows on them. The checkers the arrows hit are captured. It cannot trigger twice in one turn."
    ///
    /// Its first move of a turn arms a volley of one arrow per point moved — a compound move counts its first die —
    /// and GameState waits for its owner to pick the targets (<see cref="GameState.TryFireVolley"/>) before anything
    /// else is played. Any of the opponent's checkers on the board can be picked, on a made point too, but not an
    /// Armored one. It shoots from the board: a Bowman a tile has captured on landing shoots nothing, and that move
    /// still was its one chance of the turn.
    /// </summary>
    public class BowmanChecker : SpecialChecker, IMoveReactor
    {
        public const string Id = "checker_bowman";

        // The roll of the turn it last moved in. Every turn rolls new dice, so the same roll means the same turn.
        private DiceRoll _lastMoveRoll;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            DiceRoll roll = context.CurrentRoll;
            if (roll != null && ReferenceEquals(roll, _lastMoveRoll))
                return;

            _lastMoveRoll = roll;

            int point = context.Board.FindNodeIndexOf(this);
            if (GameState.IsOnBoard(point) && context.Board.ArmVolley(this, result.Distance))
                context.Board.ReportAbility(AbilityIds.BowmanVolley, this, point);
        }

        public BowmanChecker(int ownerId) : base(ownerId, Id, "Bowman Checker") { }
    }
}

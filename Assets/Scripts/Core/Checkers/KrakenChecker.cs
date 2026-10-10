using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Kraken Checker. "Once per turn: if you roll a double, the Kraken wakes up! Every open (single) enemy checker
    /// on the board is captured at once and sent to the bar. You do not even have to play the dice."
    /// It reacts to the roll itself, so "once per turn" holds by construction, and the dice stay in hand.
    /// It must be on the board to wake — not on the bar.
    /// </summary>
    public class KrakenChecker : SpecialChecker, IRollReactor
    {
        public const string Id = "checker_kraken";

        public void OnDiceRolled(IMatchContext context, DiceRoll roll)
        {
            if (!roll.IsDouble)
                return;

            GameState board = context.Board;
            board.ReportAbility(AbilityIds.KrakenAwakens, this, board.FindNodeIndexOf(this));

            for (int i = 0; i < GameState.NodeCount && !board.IsMatchOver; i++)
            {
                // Only an open checker: a single enemy that the rules would let us capture.
                Node node = board.Nodes[i];
                if (node.CountEnemiesOf(OwnerId) == 1 && !node.IsBlockedFor(OwnerId))
                    board.ForceCapture(node.GetTopEnemyOf(OwnerId), i, OwnerId);
            }
        }

        public KrakenChecker(int ownerId) : base(ownerId, Id, "Kraken Checker") { }
    }
}

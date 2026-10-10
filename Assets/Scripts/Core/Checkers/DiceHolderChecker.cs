using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Dice Holder Checker. "If you move this checker with your first die, you get to roll your second (remaining)
    /// die again."
    /// Only an ordinary pair of dice has a "second die": a double, a single poisoned die or the free split of
    /// Cartographer's Cheat leaves nothing to roll again.
    /// </summary>
    public class DiceHolderChecker : SpecialChecker, IMoveReactor
    {
        public const string Id = "checker_diceholder";

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            DiceRoll roll = context.CurrentRoll;
            if (!isMover || roll == null || roll.FreeSplit || roll.IsDouble)
                return;

            // "With your first die": this was the turn's first move and the other die is still in hand.
            if (roll.MovesSpent != 1 || roll.RemainingMoves.Count != 1)
                return;

            context.RerollRemainingDice(OwnerId);
            context.Board.ReportAbility(AbilityIds.DiceHolderReroll, this, result.ToIndex);
        }

        public DiceHolderChecker(int ownerId) : base(ownerId, Id, "Dice Holder Checker") { }
    }
}

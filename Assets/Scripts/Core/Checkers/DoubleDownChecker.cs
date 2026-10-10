using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Double Down Checker. "When you move it, there is a 25% chance that one of this turn's dice
    /// counts as a double (an extra move)."
    /// </summary>
    public class DoubleDownChecker : SpecialChecker, IMoveReactor
    {
        public const string Id = "checker_doubledown";
        private const float BonusChance = 0.25f;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover || context.CurrentRoll == null)
                return;

            if (!context.Random.Chance(BonusChance))
                return;

            // Extra move worth the first die of the roll.
            int bonusValue = context.CurrentRoll.First;
            context.CurrentRoll.GrantExtraMove(bonusValue);
            context.Board.ReportAbility(AbilityIds.DoubleDownBonus, this, result.ToIndex);
        }

        public DoubleDownChecker(int ownerId) : base(ownerId, Id, "Double Down Checker") { }
    }
}

using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Double Down Checker. "25% chance that one of this turn's dice counts twice (an extra move),
    /// 50% chance that you lose all remaining moves of this turn."
    /// The classic "high risk / high reward" checker.
    /// </summary>
    public class DoubleDownChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_doubledown";

        private const int BonusThreshold = 25;   // 25% reward
        private const int PenaltyThreshold = 75; // the next 50% penalty

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover || context.CurrentRoll == null)
                return;

            int roll = context.Random.Range(0, 100);

            if (roll < BonusThreshold)
            {
                // Extra move worth the first die of the roll.
                int bonusValue = context.CurrentRoll.First;
                context.CurrentRoll.GrantExtraMove(bonusValue);
                context.Board.ReportAbility(AbilityIds.DoubleDownBonus, this, result.ToIndex);
            }
            else if (roll < PenaltyThreshold)
            {
                context.CurrentRoll.LoseRemainingMoves();
                context.Board.ReportAbility(AbilityIds.DoubleDownPenalty, this, result.ToIndex);
            }
        }

        public string ItemId => Id;
        public string DisplayName => "Double Down Checker";
        public int Price => 380;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public DoubleDownChecker(int ownerId) : base(ownerId) { }
    }
}

using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Time Bender Checker. "Every time you move it there is a 50% chance the opponent's turn is skipped,
    /// and a 50% chance that nothing happens."
    /// </summary>
    public class TimeBenderChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_timebender";
        private const float SkipChance = 0.5f;

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            if (!context.Random.Chance(SkipChance))
                return;

            int opponentId = (OwnerId + 1) % GameState.PlayerCount;
            context.SkipTurn(opponentId);
            context.Board.ReportAbility(AbilityIds.TimeBend, this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Time Bender Checker";
        public int Price => 550;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public TimeBenderChecker(int ownerId) : base(ownerId) { }
    }
}

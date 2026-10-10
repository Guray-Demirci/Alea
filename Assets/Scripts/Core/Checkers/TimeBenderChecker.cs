using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Time Bender Checker. "Every time you move it there is a 50% chance the opponent's turn is skipped,
    /// and a 50% chance that nothing happens."
    /// </summary>
    public class TimeBenderChecker : SpecialChecker, IMoveReactor
    {
        public const string Id = "checker_timebender";
        private const float SkipChance = 0.5f;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            if (!context.Random.Chance(SkipChance))
                return;

            context.SkipTurn(GameState.GetOpponent(OwnerId));
            context.Board.ReportAbility(AbilityIds.TimeBend, this, result.ToIndex);
        }

        public TimeBenderChecker(int ownerId) : base(ownerId, Id, "Time Bender Checker") { }
    }
}

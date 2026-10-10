using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Golden Checker. "When it is borne off, it gives the player 2 gold."
    /// Bearing off is not a move onto a point, so the hook is <see cref="IBearOffReactor"/>, not IMoveReactor.
    /// </summary>
    public class GoldenChecker : SpecialChecker, IBearOffReactor
    {
        public const string Id = "checker_golden";
        private const int GoldOnBearOff = 2;

        public void OnBorneOff(IMatchContext context, MoveResult result)
        {
            context.AddGold(OwnerId, GoldOnBearOff);
            context.Board.ReportAbility(AbilityIds.GoldenIncome, this, result.FromIndex);
        }

        public GoldenChecker(int ownerId) : base(ownerId, Id, "Golden Checker") { }
    }
}

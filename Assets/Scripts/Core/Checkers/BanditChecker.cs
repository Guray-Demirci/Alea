using Tavla.Core.Abilities;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Bandit Checker. "For every enemy checker that passes over it (even when jumping over),
    /// +1 gold is instantly credited to you. A toll gate on the opponent's escape routes."
    /// Classic backgammon never inspects the points IN BETWEEN; for this checker a pass-over scan
    /// step (NotifyPassOver) had to be added to GameState.
    /// </summary>
    public class BanditChecker : SpecialChecker, IPassOverReactor
    {
        public const string Id = "checker_bandit";
        private const int TollAmount = 1;

        public void OnEnemyPassedOver(IMatchContext context, BaseChecker passingChecker, int nodeIndex)
        {
            context.AddGold(OwnerId, TollAmount);
            context.Board.ReportAbility(AbilityIds.TollCollected, this, nodeIndex);
        }

        public BanditChecker(int ownerId) : base(ownerId, Id, "Bandit Checker") { }
    }
}

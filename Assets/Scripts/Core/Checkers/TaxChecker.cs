using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Tax Checker. "For every enemy checker that passes over it (even when jumping over),
    /// +1 gold is instantly credited to you. A toll gate on the opponent's escape routes."
    /// Classic backgammon never inspects the points IN BETWEEN; for this checker a pass-over scan
    /// step (NotifyPassOver) had to be added to GameState.
    /// </summary>
    public class TaxChecker : BaseChecker, IPassOverReactor, IPermanent
    {
        public const string Id = "checker_tax";
        private const int TollAmount = 1;

        public override string TypeId => Id;

        public void OnEnemyPassedOver(IMatchContext context, BaseChecker passingChecker, int nodeIndex)
        {
            context.AddGold(OwnerId, TollAmount);
            context.Board.ReportAbility(AbilityIds.TollCollected, this, nodeIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Tax Checker";
        public int Price => 340;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public TaxChecker(int ownerId) : base(ownerId) { }
    }
}

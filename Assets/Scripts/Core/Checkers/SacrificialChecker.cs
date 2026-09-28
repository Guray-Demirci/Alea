using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Sacrificial Checker. "When the opponent captures this checker it does not go to the bar, it is DESTROYED.
    /// In exchange every item in the shop becomes permanently 2 gold cheaper."
    /// Two responsibilities, two interfaces:
    ///   <see cref="ICustomCaptureDestination"/> -> WHERE it goes
    ///   <see cref="ICaptureReactor"/>           -> what the SIDE EFFECT is
    /// </summary>
    public class SacrificialChecker : BaseChecker, ICustomCaptureDestination, ICaptureReactor, IPermanent
    {
        public const string Id = "checker_sacrificial";
        private const int DiscountAmount = 2;

        public override string TypeId => Id;

        public CaptureDestination Destination => CaptureDestination.Destroyed;

        public void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex)
        {
            context.AddShopDiscount(DiscountAmount);
            context.Board.ReportAbility(AbilityIds.SacrificeDiscount, this, nodeIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Sacrificial Checker";
        public int Price => 150;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public SacrificialChecker(int ownerId) : base(ownerId) { }
    }
}

using Tavla.Core.Abilities;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Sacrificial Checker. "When the opponent captures this checker it does not go to the bar, it is DESTROYED.
    /// In exchange every item in the shop becomes permanently 1 gold cheaper."
    /// Two responsibilities, two interfaces:
    ///   <see cref="ICustomCaptureDestination"/> -> WHERE it goes
    ///   <see cref="ICaptureReactor"/>           -> what the SIDE EFFECT is
    /// </summary>
    public class SacrificialChecker : SpecialChecker, ICustomCaptureDestination, ICaptureReactor
    {
        public const string Id = "checker_sacrificial";
        private const int DiscountAmount = 1;

        public CaptureDestination Destination => CaptureDestination.Destroyed;

        public void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex)
        {
            context.AddShopDiscount(DiscountAmount);
            context.Board.ReportAbility(AbilityIds.SacrificeDiscount, this, nodeIndex);
        }

        public SacrificialChecker(int ownerId) : base(ownerId, Id, "Sacrificial Checker") { }
    }
}

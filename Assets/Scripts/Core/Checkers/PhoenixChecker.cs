using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Phoenix Checker. A two-layer checker:
    ///   Board rule : when captured it does NOT go to the bar, it returns to its own starting point.
    ///   Meta rule  : every capture triggers "Rise from the Ashes" -> the next shop offers only
    ///                Rare/Epic/Legendary items at a 50% discount.
    /// </summary>
    public class PhoenixChecker : BaseChecker, ICustomCaptureDestination, ICaptureReactor, IPermanent
    {
        public const string Id = "checker_phoenix";

        /// <summary>How many times it has risen from the ashes — lets the View grow its feather/flame effect.</summary>
        public int RebirthCount { get; private set; }

        public override string TypeId => Id;

        public CaptureDestination Destination => CaptureDestination.HomeEntry;

        public void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex)
        {
            RebirthCount++;
            context.TriggerLuxuryShop(OwnerId);
            context.Board.ReportAbility(AbilityIds.PhoenixRebirth, this, nodeIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Phoenix Checker";
        public int Price => 900;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public PhoenixChecker(int ownerId) : base(ownerId) { }
    }
}

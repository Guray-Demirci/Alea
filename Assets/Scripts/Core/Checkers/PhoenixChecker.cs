using Tavla.Core.Abilities;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Phoenix Checker. A two-layer checker:
    ///   Board rule : when captured it does NOT go to the bar, it returns to its own starting point —
    ///                or, if that point is full, to the first point after it with room.
    ///   Meta rule  : every capture triggers "Rise from the Ashes" -> the next shop offers only
    ///                Rare/Epic/Legendary items.
    /// </summary>
    public class PhoenixChecker : SpecialChecker, ICustomCaptureDestination, ICaptureReactor
    {
        public const string Id = "checker_phoenix";

        /// <summary>How many times it has risen from the ashes — lets the View grow its feather/flame effect.</summary>
        public int RebirthCount { get; private set; }

        public CaptureDestination Destination => CaptureDestination.HomeEntry;

        public void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex)
        {
            RebirthCount++;
            context.TriggerLuxuryShop(OwnerId);
            context.Board.ReportAbility(AbilityIds.PhoenixRebirth, this, nodeIndex);
        }

        public PhoenixChecker(int ownerId) : base(ownerId, Id, "Phoenix Checker") { }
    }
}

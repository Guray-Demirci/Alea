using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Catalyst Checker. "The moment it captures an enemy checker, all points you earn this turn are doubled."
    /// No separate "I captured" interface was needed: MoveResult already carries the captured checkers.
    /// </summary>
    public class CatalystChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_catalyst";
        private const float CaptureMultiplier = 2f;

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover || !result.HasCapture)
                return;

            // permanent: false -> valid for this turn only.
            context.AddScoreMultiplier(OwnerId, CaptureMultiplier, false);
            context.Board.ReportAbility(AbilityIds.Catalyst, this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Catalyst Checker";
        public int Price => 480;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public CatalystChecker(int ownerId) : base(ownerId) { }
    }
}

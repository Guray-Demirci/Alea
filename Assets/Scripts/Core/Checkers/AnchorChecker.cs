using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Anchor Checker. "When moved, one of the two dice of your next roll is pinned to the number
    /// of points it moved. Removes luck entirely."
    /// The only checker that touches the dice system; the bridge is <see cref="IMatchContext.PinNextDie"/>.
    /// </summary>
    public class AnchorChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_anchor";

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            int distance = result.ToIndex - result.FromIndex;
            if (distance < 0)
                distance = -distance;

            // Do not pin an invalid value in edge cases such as entering from the bar.
            if (distance < 1 || distance > GameState.MaxDieValue)
                return;

            context.PinNextDie(OwnerId, distance);
            context.Board.ReportAbility(AbilityIds.AnchorPin, this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Anchor Checker";
        public int Price => 620;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public AnchorChecker(int ownerId) : base(ownerId) { }
    }
}

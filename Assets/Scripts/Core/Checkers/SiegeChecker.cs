using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Siege Checker. "When another checker of your colour arrives on this checker's point and makes a POINT,
    /// the multiplier of all your moves becomes PERMANENTLY x2."
    /// This checker is the reason the isMover=false hook exists: the trigger is not its own move
    /// but the move of a friendly checker.
    /// </summary>
    public class SiegeChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_siege";
        private const float SiegeMultiplier = 2f;

        /// <summary>The permanent multiplier is granted once; otherwise it would compound on every move.</summary>
        private bool _triggered;

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            // Must be a friendly checker's move, not our own.
            if (isMover || _triggered || result.PlayerId != OwnerId)
                return;

            // Was a point made? (this checker + the arriving friendly one = at least 2)
            Node node = context.Board.Nodes[result.ToIndex];
            if (node.CountFor(OwnerId) < 2)
                return;

            _triggered = true;
            context.AddScoreMultiplier(OwnerId, SiegeMultiplier, true);
            context.Board.ReportAbility(AbilityIds.SiegeLocked, this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Siege Checker";
        public int Price => 650;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public SiegeChecker(int ownerId) : base(ownerId) { }
    }
}

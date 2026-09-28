using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Navigator Checker. "Leaves footprints behind as it advances.
    /// Grants 50 base points for EVERY NEW point it lands on."
    /// "New" matters: landing on the same point again scores nothing, so the checker must
    /// remember the points it has visited (the state belongs to the checker itself, SRP).
    /// </summary>
    public class NavigatorChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_navigator";
        private const int ScorePerNewNode = 50;

        private readonly HashSet<int> _visited = new HashSet<int>();

        public override string TypeId => Id;

        /// <summary>Visited points, so the View can draw the footprint trail.</summary>
        public IEnumerable<int> VisitedNodes => _visited;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            // Add returns false if the point was visited before -> no score.
            if (!_visited.Add(result.ToIndex))
                return;

            context.AddScore(OwnerId, ScorePerNewNode);
            context.Board.ReportAbility(AbilityIds.NavigatorTrail, this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Navigator Checker";
        public int Price => 320;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public NavigatorChecker(int ownerId) : base(ownerId) { }
    }
}

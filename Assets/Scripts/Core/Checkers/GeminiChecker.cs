using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Gemini Checker. "Always sold as a PAIR in the shop. If you manage to bring both checkers
    /// onto two adjacent points you earn extra score and a multiplier."
    /// It recognizes its twin through <see cref="PairId"/>; it must never be created alone outside <see cref="CreatePair"/>.
    /// </summary>
    public class GeminiChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_gemini";

        private const int AdjacencyScore = 200;
        private const float AdjacencyMultiplier = 1.5f;

        /// <summary>Two checkers with the same PairId are twins.</summary>
        public int PairId { get; private set; }

        public override string TypeId => Id;

        private GeminiChecker(int ownerId, int pairId) : base(ownerId)
        {
            PairId = pairId;
        }

        /// <summary>The shop cannot sell this checker alone — it is always created as a pair.</summary>
        public static GeminiChecker[] CreatePair(int ownerId, int pairId)
        {
            return new[]
            {
                new GeminiChecker(ownerId, pairId),
                new GeminiChecker(ownerId, pairId)
            };
        }

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            // Only the moving twin checks, so the bonus is not written twice.
            if (!isMover)
                return;

            int myIndex = result.ToIndex;
            if (!IsTwinAt(context, myIndex - 1) && !IsTwinAt(context, myIndex + 1))
                return;

            context.AddScore(OwnerId, AdjacencyScore);
            context.AddScoreMultiplier(OwnerId, AdjacencyMultiplier, false);
            context.Board.ReportAbility(AbilityIds.GeminiLink, this, myIndex);
        }

        private bool IsTwinAt(IMatchContext context, int index)
        {
            if (!GameState.IsOnBoard(index))
                return false;

            Node node = context.Board.Nodes[index];
            for (int i = 0; i < node.CheckerCount; i++)
            {
                GeminiChecker twin = node.Checkers[i] as GeminiChecker;
                if (twin != null && !ReferenceEquals(twin, this) && twin.PairId == PairId)
                    return true;
            }
            return false;
        }

        public string ItemId => Id;
        public string DisplayName => "Gemini Checker (Pair)";
        public int Price => 700;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;
    }
}

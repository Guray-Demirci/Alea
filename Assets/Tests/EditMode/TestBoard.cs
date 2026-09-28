using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tests
{
    /// <summary>
    /// Deterministic randomness: Range always returns the lower bound and Chance never succeeds,
    /// so chance-based abilities stay quiet unless a test opts in.
    /// </summary>
    internal sealed class FixedRandom : IRandomProvider
    {
        public int Range(int minInclusive, int maxExclusive) => minInclusive;

        public bool Chance(float probability) => false;
    }

    /// <summary>Helpers to build hand-crafted positions for rule tests.</summary>
    internal static class TestBoard
    {
        /// <summary>An empty board bound to a real MatchContext with deterministic randomness.</summary>
        public static GameState CreateEmpty(out MatchContext context, int turnLimit = GameState.DefaultTurnLimit)
        {
            GameState state = new GameState(turnLimit);
            context = new MatchContext(state, new FixedRandom());
            return state;
        }

        /// <summary>An empty board playing under the given rules, bound to a real MatchContext.</summary>
        public static GameState CreateWithRules(MatchRules rules, out MatchContext context)
        {
            GameState state = new GameState(rules);
            context = new MatchContext(state, new FixedRandom());
            return state;
        }

        /// <summary>Rolls for the current player and then forces the given dice.</summary>
        public static void SetDice(MatchContext context, int first, int second)
        {
            context.RollDice(context.Board.CurrentPlayerId);
            context.CurrentRoll.Set(first, second);
        }

        public static T Place<T>(GameState state, int nodeIndex, T checker) where T : BaseChecker
        {
            state.Nodes[nodeIndex].Push(checker);
            return checker;
        }

        public static DefaultChecker PlaceDefault(GameState state, int nodeIndex, int ownerId)
        {
            return Place(state, nodeIndex, new DefaultChecker(ownerId));
        }
    }
}

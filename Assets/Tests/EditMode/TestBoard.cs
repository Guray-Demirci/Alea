using System.Collections.Generic;
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

    /// <summary>
    /// Like <see cref="FixedRandom"/>, but every chance roll succeeds — for abilities and tiles that only
    /// act on a lucky roll (Double Down, Clover).
    /// </summary>
    internal sealed class LuckyRandom : IRandomProvider
    {
        public int Range(int minInclusive, int maxExclusive) => minInclusive;

        public bool Chance(float probability) => true;
    }

    /// <summary>
    /// Like <see cref="FixedRandom"/>, but Range walks up from the lower bound (min, min+1, ...), so a roll of two
    /// dice comes out 1-2 — for rules that must see a roll that is NOT a double (Kraken).
    /// </summary>
    internal sealed class SteppingRandom : IRandomProvider
    {
        private int _step;

        public int Range(int minInclusive, int maxExclusive) => minInclusive + _step++ % (maxExclusive - minInclusive);

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

        /// <summary>An empty board bound to a real MatchContext that draws from the given randomness.</summary>
        public static GameState CreateEmpty(IRandomProvider random, out MatchContext context)
        {
            GameState state = new GameState();
            context = new MatchContext(state, random);
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

        /// <summary>Both players with the most checkers they can field, dealt deterministically.</summary>
        public static StartingLayout SetupFullBoard(GameState state)
        {
            const int count = GameState.MaxCheckersPerPlayer;
            StartingLayout layout = StartingLayout.Deal(count, count, new FixedRandom());

            for (int p = 0; p < GameState.PlayerCount; p++)
                state.SetupPlayer(p, CreateDefaults(p, count), layout);

            return layout;
        }

        public static List<BaseChecker> CreateDefaults(int ownerId, int count)
        {
            List<BaseChecker> checkers = new List<BaseChecker>(count);
            for (int i = 0; i < count; i++)
                checkers.Add(new DefaultChecker(ownerId));

            return checkers;
        }

        /// <summary>How many of the player's checkers stand on each point.</summary>
        public static int[] CountPerPoint(GameState state, int playerId)
        {
            int[] counts = new int[GameState.NodeCount];
            for (int i = 0; i < counts.Length; i++)
                counts[i] = state.Nodes[i].CountFor(playerId);

            return counts;
        }
    }
}

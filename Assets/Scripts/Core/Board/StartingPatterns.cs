using System;
using System.Collections.Generic;

namespace Tavla.Core.Board
{
    /// <summary>
    /// The designed starting formations: for every checker count, the stacks the checkers may be dealt in.
    /// Each number is one stack on its own point; a count with several formations gets one of them at random
    /// (<see cref="StartingLayout.Deal"/>). Where the stacks go is not part of the formation.
    ///
    /// No stack is higher than <see cref="GameState.StackLimit"/>, so every formation can be played.
    /// </summary>
    public static class StartingPatterns
    {
        // Indexed by checker count - MinCheckersPerPlayer. The designer's table; edit it here.
        private static readonly int[][][] Table =
        {
            /*  5 */ new[] { new[] { 2, 3 } },
            /*  6 */ new[] { new[] { 2, 2, 2 }, new[] { 3, 3 } },
            /*  7 */ new[] { new[] { 2, 2, 3 }, new[] { 4, 3 } },
            /*  8 */ new[] { new[] { 3, 3, 2 }, new[] { 4, 2, 2 } },
            /*  9 */ new[] { new[] { 4, 3, 2 }, new[] { 3, 3, 3 }, new[] { 2, 2, 3, 2 } },
            /* 10 */ new[] { new[] { 4, 4, 2 }, new[] { 3, 3, 4 }, new[] { 2, 2, 3, 3 } },
            /* 11 */ new[] { new[] { 4, 4, 3 }, new[] { 2, 3, 3, 3 } },
            /* 12 */ new[] { new[] { 3, 3, 3, 3 }, new[] { 2, 2, 4, 4 }, new[] { 4, 3, 3, 2 } },
            /* 13 */ new[] { new[] { 4, 3, 3, 3 }, new[] { 4, 4, 3, 2 } },
            /* 14 */ new[] { new[] { 4, 4, 3, 3 }, new[] { 4, 4, 4, 2 } },
            /* 15 */ new[] { new[] { 4, 4, 4, 3 } }
        };

        // Read-only views of the table, so callers cannot edit a formation through the returned lists.
        private static readonly IReadOnlyList<IReadOnlyList<int>>[] Formations = BuildFormations();

        /// <summary>Most stacks any formation uses. <see cref="StartingLayout"/> sizes its free-point check by it.</summary>
        public static int MaxStacks { get; } = CountMaxStacks();

        /// <summary>The formations a side with this many checkers can be dealt in. Never empty.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The count is outside <see cref="GameState.MinCheckersPerPlayer"/>..<see cref="GameState.MaxCheckersPerPlayer"/>.
        /// </exception>
        public static IReadOnlyList<IReadOnlyList<int>> Get(int checkerCount)
        {
            if (checkerCount < GameState.MinCheckersPerPlayer || checkerCount > GameState.MaxCheckersPerPlayer)
                throw new ArgumentOutOfRangeException(nameof(checkerCount), checkerCount,
                    $"A side starts with {GameState.MinCheckersPerPlayer}..{GameState.MaxCheckersPerPlayer} checkers.");

            return Formations[checkerCount - GameState.MinCheckersPerPlayer];
        }

        private static IReadOnlyList<IReadOnlyList<int>>[] BuildFormations()
        {
            IReadOnlyList<IReadOnlyList<int>>[] formations = new IReadOnlyList<IReadOnlyList<int>>[Table.Length];
            for (int c = 0; c < Table.Length; c++)
            {
                IReadOnlyList<int>[] options = new IReadOnlyList<int>[Table[c].Length];
                for (int o = 0; o < options.Length; o++)
                    options[o] = Array.AsReadOnly(Table[c][o]);

                formations[c] = Array.AsReadOnly(options);
            }

            return formations;
        }

        private static int CountMaxStacks()
        {
            int max = 0;
            for (int c = 0; c < Table.Length; c++)
            {
                for (int o = 0; o < Table[c].Length; o++)
                    max = Math.Max(max, Table[c][o].Length);
            }

            return max;
        }
    }
}

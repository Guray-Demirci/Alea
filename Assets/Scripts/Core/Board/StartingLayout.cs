using System;
using System.Collections.Generic;
using Tavla.Core.Runtime;

namespace Tavla.Core.Board
{
    /// <summary>
    /// Where every checker of BOTH players starts in one match. Dealt fresh for every match by <see cref="Deal"/>
    /// and immutable afterwards; <see cref="GameState.SetupPlayer"/> places the checkers on it.
    ///
    /// Dealing rules:
    ///   - each side gets one of the <see cref="StartingPatterns"/> formations for its checker count, at random;
    ///   - every stack goes to its own empty point, chosen at random;
    ///   - no checker starts in its own home board, so nothing can be borne off before it has moved;
    ///   - blocked points (the run's tiles) stay empty, so no checker starts on a trap.
    /// Both sides are dealt together because they share the board: the two players pick points in turn, so
    /// neither gets first choice of the whole middle of the board.
    ///
    /// The randomness comes from the match's <see cref="IRandomProvider"/>, so a seed reproduces the layout too.
    /// </summary>
    public sealed class StartingLayout
    {
        // [player][slot] -> point. Slot k is the k-th checker handed to SetupPlayer.
        private readonly int[][] _slotPoints;

        private StartingLayout(int[][] slotPoints)
        {
            _slotPoints = slotPoints;
        }

        /// <summary>Checkers this layout places for the player.</summary>
        public int GetCheckerCount(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);
            return _slotPoints[playerId].Length;
        }

        /// <summary>The point the player's checker in the given slot starts on.</summary>
        public int GetPoint(int playerId, int slot)
        {
            GameState.ThrowIfInvalidPlayer(playerId);

            int[] points = _slotPoints[playerId];
            if (slot < 0 || slot >= points.Length)
                throw new ArgumentOutOfRangeException(nameof(slot), slot,
                    $"Slot must be in the range 0..{points.Length - 1}.");

            return points[slot];
        }

        /// <summary>Can the player's checkers start on this point? Any point outside the player's own home board.</summary>
        public static bool IsAllowedPoint(int playerId, int point)
        {
            return GameState.IsOnBoard(point) && !GameState.IsInHomeBoard(point, playerId);
        }

        /// <summary>
        /// Is there room to deal any two formations around these blocked points? When a stack is placed, the
        /// points already taken are at most the side's own earlier stacks and all of the opponent's, so every
        /// side needs twice <see cref="StartingPatterns.MaxStacks"/> free allowed points.
        /// </summary>
        public static bool HasRoomAround(IReadOnlyCollection<int> blockedPoints)
        {
            bool[] blocked = ToBlockedMask(blockedPoints);
            int needed = 2 * StartingPatterns.MaxStacks;

            for (int p = 0; p < GameState.PlayerCount; p++)
            {
                if (CountFreeAllowedPoints(p, blocked) < needed)
                    return false;
            }

            return true;
        }

        /// <summary>Deals a new layout for one match.</summary>
        /// <param name="blockedPoints">Points no checker may start on, such as tiles. Null for none.</param>
        /// <exception cref="ArgumentNullException">No randomness was given.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A count without formations, or a blocked point off the board.</exception>
        /// <exception cref="InvalidOperationException">
        /// The blocked points leave no room (never happens when <see cref="HasRoomAround"/> is true).
        /// </exception>
        public static StartingLayout Deal(int player0Checkers, int player1Checkers, IRandomProvider random,
                                          IReadOnlyCollection<int> blockedPoints = null)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            IReadOnlyList<int>[] formations =
            {
                PickFormation(player0Checkers, nameof(player0Checkers), random),
                PickFormation(player1Checkers, nameof(player1Checkers), random)
            };

            bool[] taken = ToBlockedMask(blockedPoints);
            int[][] stackPoints = { new int[formations[0].Count], new int[formations[1].Count] };
            List<int> candidates = new List<int>(GameState.NodeCount);

            int rounds = Math.Max(formations[0].Count, formations[1].Count);
            for (int s = 0; s < rounds; s++)
            {
                for (int p = 0; p < GameState.PlayerCount; p++)
                {
                    if (s >= formations[p].Count)
                        continue;

                    candidates.Clear();
                    for (int point = 0; point < GameState.NodeCount; point++)
                    {
                        if (!taken[point] && IsAllowedPoint(p, point))
                            candidates.Add(point);
                    }

                    if (candidates.Count == 0)
                        throw new InvalidOperationException(
                            $"No free point is left for player {p}'s starting checkers; too many points are blocked.");

                    int chosen = candidates[random.Range(0, candidates.Count)];
                    taken[chosen] = true;
                    stackPoints[p][s] = chosen;
                }
            }

            int[][] slotPoints = new int[GameState.PlayerCount][];
            for (int p = 0; p < GameState.PlayerCount; p++)
                slotPoints[p] = ExpandStacks(formations[p], stackPoints[p]);

            return new StartingLayout(slotPoints);
        }

        private static IReadOnlyList<int> PickFormation(int checkerCount, string paramName, IRandomProvider random)
        {
            IReadOnlyList<IReadOnlyList<int>> options;
            try
            {
                options = StartingPatterns.Get(checkerCount);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new ArgumentOutOfRangeException(paramName, checkerCount, e.Message);
            }

            return options[random.Range(0, options.Count)];
        }

        /// <summary>Stack sizes and their points -> one point per checker, stack after stack.</summary>
        private static int[] ExpandStacks(IReadOnlyList<int> formation, int[] stackPoints)
        {
            int total = 0;
            for (int s = 0; s < formation.Count; s++)
                total += formation[s];

            int[] slots = new int[total];
            int slot = 0;
            for (int s = 0; s < formation.Count; s++)
            {
                for (int i = 0; i < formation[s]; i++)
                    slots[slot++] = stackPoints[s];
            }

            return slots;
        }

        private static bool[] ToBlockedMask(IReadOnlyCollection<int> blockedPoints)
        {
            bool[] mask = new bool[GameState.NodeCount];
            if (blockedPoints == null)
                return mask;

            foreach (int point in blockedPoints)
            {
                if (!GameState.IsOnBoard(point))
                    throw new ArgumentOutOfRangeException(nameof(blockedPoints), point,
                        $"A blocked point must be in the range 0..{GameState.NodeCount - 1}.");

                mask[point] = true;
            }

            return mask;
        }

        private static int CountFreeAllowedPoints(int playerId, bool[] blocked)
        {
            int count = 0;
            for (int point = 0; point < GameState.NodeCount; point++)
            {
                if (!blocked[point] && IsAllowedPoint(playerId, point))
                    count++;
            }

            return count;
        }
    }
}

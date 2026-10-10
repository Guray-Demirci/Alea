using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Board;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Nomad Checker. "If the points to the left and right of the point a die brings it to are empty,
    /// it may move 3 extra points." Any checker on a side blocks it, friend or foe; its own neighbours do not matter.
    /// The bonus is added to the DIE VALUE and grants no extra move: with a 3 it travels 6 points, and in its home
    /// board a 1 bears it off from 4 points away. It passes over the die's point, which only its sides decide.
    /// </summary>
    public class NomadChecker : SpecialChecker, IExtraDistanceProvider
    {
        public const string Id = "checker_nomad";
        private const int BonusDistance = 3;

        public int GetBonusDistance(GameState state, int dieTargetIndex)
        {
            // A die that already carries it off the board leaves nothing to stretch.
            if (!GameState.IsOnBoard(dieTargetIndex))
                return 0;

            return IsSideEmpty(state, dieTargetIndex - 1) && IsSideEmpty(state, dieTargetIndex + 1)
                ? BonusDistance
                : 0;
        }

        /// <summary>
        /// The board edge also counts as "empty" — no checker can block from there. So does the point the Nomad
        /// leaves (a side of a 1's point) once it is gone, but not the checkers it shared that point with.
        /// </summary>
        private bool IsSideEmpty(GameState state, int index)
        {
            if (!GameState.IsOnBoard(index))
                return true;

            IReadOnlyList<BaseChecker> checkers = state.Nodes[index].Checkers;
            for (int i = 0; i < checkers.Count; i++)
            {
                if (!ReferenceEquals(checkers[i], this))
                    return false;
            }

            return true;
        }

        public NomadChecker(int ownerId) : base(ownerId, Id, "Nomad Checker") { }
    }
}

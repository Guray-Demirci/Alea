using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Board
{
    /// <summary>
    /// A single point on the board. Holds checkers as a LIFO stack.
    ///
    /// IMPORTANT: the Parasite Checker can infiltrate enemy points, so a point may hold
    /// checkers of BOTH PLAYERS at the same time. "OwnerId" alone is therefore not enough;
    /// rule calculations use per-player counts.
    ///
    /// Mutators are <c>internal</c>: only Core may change the board. GameState routes every
    /// removal through its tile hooks, and bypassing that path from outside would leave
    /// tiles holding stale per-checker state.
    /// </summary>
    public class Node
    {
        public const int EmptyOwner = -1;

        public int Index { get; private set; }

        private readonly List<BaseChecker> _checkers = new List<BaseChecker>();

        public IReadOnlyList<BaseChecker> Checkers => _checkers;

        public int CheckerCount => _checkers.Count;

        /// <summary>
        /// The player who "holds" the point = owner of the bottom checker.
        /// A Parasite infiltration does not change the real owner; it is only a guest.
        /// </summary>
        public int OwnerId => _checkers.Count == 0 ? EmptyOwner : _checkers[0].OwnerId;

        /// <summary>Special tile installed on this point (null if none).</summary>
        public ITileEffect ActiveTile { get; private set; }

        public BaseChecker TopChecker => _checkers.Count == 0 ? null : _checkers[_checkers.Count - 1];

        public bool IsEmpty => _checkers.Count == 0;

        public Node(int index)
        {
            Index = index;
        }

        #region Counting / queries

        /// <summary>Number of the given player's checkers on this point.</summary>
        public int CountFor(int playerId)
        {
            int count = 0;
            for (int i = 0; i < _checkers.Count; i++)
            {
                if (_checkers[i].OwnerId == playerId)
                    count++;
            }
            return count;
        }

        /// <summary>Number of enemy checkers from the given player's perspective.</summary>
        public int CountEnemiesOf(int playerId) => _checkers.Count - CountFor(playerId);

        /// <summary>Topmost enemy checker from the given player's perspective (the capture target). Null if none.</summary>
        public BaseChecker GetTopEnemyOf(int playerId)
        {
            for (int i = _checkers.Count - 1; i >= 0; i--)
            {
                if (_checkers[i].OwnerId != playerId)
                    return _checkers[i];
            }
            return null;
        }

        /// <summary>Collects every enemy checker on this point for the given player (used by the Doomsday Checker).</summary>
        public List<BaseChecker> GetEnemiesOf(int playerId)
        {
            List<BaseChecker> enemies = new List<BaseChecker>();
            for (int i = 0; i < _checkers.Count; i++)
            {
                if (_checkers[i].OwnerId != playerId)
                    enemies.Add(_checkers[i]);
            }
            return enemies;
        }

        /// <summary>
        /// Is this point closed to the given player?
        /// Three reasons: (1) the opponent has 2+ checkers, (2) an Armored Checker counts as a point on its own,
        /// (3) the single enemy checker cannot be captured.
        /// Abilities are queried through GetAbility, so they are all disabled automatically inside Fog.
        /// </summary>
        public bool IsBlockedFor(int playerId)
        {
            int enemyCount = CountEnemiesOf(playerId);
            if (enemyCount == 0)
                return false;

            // While the Dungeon tile is closed no point can be made here -> no blockade either.
            bool blockadeAllowed = ActiveTile == null || ActiveTile.AllowsBlockade;

            if (enemyCount >= 2)
                return blockadeAllowed;

            BaseChecker enemy = GetTopEnemyOf(playerId);

            IBlockadeOverride solo = enemy.GetAbility<IBlockadeOverride>();
            if (solo != null && solo.FormsBlockadeAlone && blockadeAllowed)
                return true;

            // A single checker that cannot be captured (Armored) still cannot be passed.
            return !enemy.CanBeCaptured;
        }

        /// <summary>
        /// Effective checker capacity of this point. The Vortex Checker removes the limit.
        /// The ability of ANY checker on the point may relax the limit.
        /// </summary>
        public int GetStackLimit(int defaultLimit)
        {
            int limit = defaultLimit;

            for (int i = 0; i < _checkers.Count; i++)
            {
                IStackLimitOverride limitOverride = _checkers[i].GetAbility<IStackLimitOverride>();
                if (limitOverride == null)
                    continue;

                int candidate = limitOverride.GetStackLimit(defaultLimit);
                if (candidate > limit)
                    limit = candidate;
            }

            return limit;
        }

        #endregion

        #region Mutators (Core only)

        internal void Push(BaseChecker checker)
        {
            _checkers.Add(checker);
        }

        internal BaseChecker Pop()
        {
            if (_checkers.Count == 0)
                return null;

            BaseChecker top = _checkers[_checkers.Count - 1];
            _checkers.RemoveAt(_checkers.Count - 1);
            return top;
        }

        /// <summary>Removes a specific checker (for targeted effects such as Doomsday, Spike Pit, Maelstrom).</summary>
        internal bool Remove(BaseChecker checker)
        {
            return _checkers.Remove(checker);
        }

        internal void SetTile(ITileEffect effect)
        {
            ActiveTile = effect;
        }

        internal void ClearTile()
        {
            ActiveTile = null;
        }

        #endregion
    }
}

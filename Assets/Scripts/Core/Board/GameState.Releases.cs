using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Board
{
    // Freeing a locked checker by sacrificing a die (Swamp).
    public partial class GameState
    {
        /// <summary>
        /// Can the current player free a locked checker on this point right now? Does NOT touch the board or the dice.
        /// The View uses it to light up the point, the bot to decide to free its checker.
        /// </summary>
        public bool CanReleaseChecker(int nodeIndex)
        {
            return ResolveRelease(nodeIndex, CurrentPlayerId, false, null);
        }

        /// <summary>Can the current player free THIS locked checker right now? Does NOT touch the board or the dice.</summary>
        public bool CanReleaseChecker(BaseChecker checker)
        {
            return checker != null && ResolveRelease(FindNodeIndexOf(checker), CurrentPlayerId, false, checker);
        }

        /// <summary>
        /// Frees the current player's locked checker on this point — "sacrifice one of your dice to unlock it".
        /// The smallest die that can pay is spent, so the larger ones stay for moves. This is not a move:
        /// the checker stays where it is and may move with the remaining dice.
        /// </summary>
        public bool TryReleaseChecker(int nodeIndex)
        {
            return ResolveRelease(nodeIndex, CurrentPlayerId, true, null);
        }

        /// <summary>Frees THIS locked checker — the one the player picked up — as <see cref="TryReleaseChecker(int)"/>.</summary>
        public bool TryReleaseChecker(BaseChecker checker)
        {
            return checker != null && ResolveRelease(FindNodeIndexOf(checker), CurrentPlayerId, true, checker);
        }

        /// <param name="chosen">The checker to free; null for the player's topmost one the tile will free.</param>
        private bool ResolveRelease(int nodeIndex, int playerId, bool commit, BaseChecker chosen)
        {
            if (IsMatchOver || IsAwaitingVolley || !IsOnBoard(nodeIndex))
                return false;

            // As with bearing off, the bar comes first: nothing else may be played while a checker waits there.
            if (GetBarCount(playerId) > 0)
                return false;

            Node node = _nodes[nodeIndex];
            IReleasableLock releasableLock = node.ActiveTile as IReleasableLock;
            if (releasableLock == null)
                return false;

            BaseChecker locked = FindReleasableChecker(node, releasableLock, playerId, chosen);
            if (locked == null)
                return false;

            int die = FindSmallestAffordableDie();
            if (die == 0)
                return false;

            if (!commit)
                return true;

            ConsumeDistance(die);

            // The tile object is shared with cloned boards, so a simulation must not clear the real lock.
            if (SideEffectsEnabled)
                releasableLock.Release(Context, node, locked);

            CheckerReleased?.Invoke(locked, nodeIndex);
            return true;
        }

        /// <summary>
        /// The player's topmost checker on the point that the tile is willing to free — or, if one was chosen,
        /// that checker when the tile will free it; null if none.
        /// </summary>
        private static BaseChecker FindReleasableChecker(Node node, IReleasableLock releasableLock, int playerId,
                                                         BaseChecker chosen)
        {
            for (int i = node.CheckerCount - 1; i >= 0; i--)
            {
                BaseChecker checker = node.Checkers[i];
                if (checker.OwnerId == playerId && (chosen == null || ReferenceEquals(checker, chosen)) &&
                    releasableLock.CanRelease(checker))
                    return checker;
            }

            return null;
        }

        /// <summary>
        /// Smallest die value the current roll can pay; 0 if none. With Cartographer's Cheat active the dice
        /// are one pool, so the sacrifice costs a single point of it.
        /// </summary>
        private int FindSmallestAffordableDie()
        {
            for (int value = 1; value <= MaxDieValue; value++)
            {
                if (CanAffordDistance(value))
                    return value;
            }

            return 0;
        }
    }
}

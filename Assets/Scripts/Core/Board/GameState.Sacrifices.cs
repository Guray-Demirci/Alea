using Tavla.Core.Abilities;
using Tavla.Core.Checkers;

namespace Tavla.Core.Board
{
    // Removing a checker from play to use its ability (Second Chance).
    public partial class GameState
    {
        /// <summary>
        /// Can the current player sacrifice their checker on this point right now? Does NOT touch the board.
        /// The View uses it to light up the point: dropping the checker back on it sacrifices it.
        /// </summary>
        public bool CanSacrificeChecker(int nodeIndex)
        {
            return ResolveSacrifice(nodeIndex, CurrentPlayerId, false, null);
        }

        /// <summary>Can the current player sacrifice THIS checker right now? Does NOT touch the board.</summary>
        public bool CanSacrificeChecker(BaseChecker checker)
        {
            return checker != null && ResolveSacrifice(FindNodeIndexOf(checker), CurrentPlayerId, false, checker);
        }

        /// <summary>
        /// Sacrifices the current player's topmost checker on this point if its ability is used that way. It leaves
        /// play for good. This is not a move and no die pays for it, so it can be done at any moment of the
        /// player's turn.
        /// </summary>
        public bool TrySacrificeChecker(int nodeIndex)
        {
            return ResolveSacrifice(nodeIndex, CurrentPlayerId, true, null);
        }

        /// <summary>Sacrifices THIS checker — the one the player picked up — as <see cref="TrySacrificeChecker(int)"/>.</summary>
        public bool TrySacrificeChecker(BaseChecker checker)
        {
            return checker != null && ResolveSacrifice(FindNodeIndexOf(checker), CurrentPlayerId, true, checker);
        }

        /// <param name="chosen">The checker to sacrifice; null for the player's topmost one on the point.</param>
        private bool ResolveSacrifice(int nodeIndex, int playerId, bool commit, BaseChecker chosen)
        {
            if (IsMatchOver || !IsOnBoard(nodeIndex))
                return false;

            BaseChecker checker = PeekSource(nodeIndex, playerId, chosen);
            ISacrificeAbility ability = checker != null ? checker.GetAbility<ISacrificeAbility>() : null;
            if (ability == null)
                return false;

            if (!commit)
                return true;

            RemoveFromNode(_nodes[nodeIndex], checker);

            // The ability first, so the listeners below already see what it bought (an extra die).
            if (SideEffectsEnabled)
                ability.OnSacrificed(Context, nodeIndex);

            CheckerDestroyed?.Invoke(checker, nodeIndex);
            CheckerSacrificed?.Invoke(checker, nodeIndex);
            CheckerLostForGood?.Invoke(checker, nodeIndex);

            // It may have been the player's last checker in play.
            HandleCheckerLeftPlay(playerId);
            return true;
        }
    }
}

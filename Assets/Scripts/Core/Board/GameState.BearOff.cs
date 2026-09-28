using System.Collections.Generic;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Board
{
    // Bearing off (removing checkers from the board for points).
    public partial class GameState
    {
        /// <summary>Is this point inside the given player's home board (the last 6 points)?</summary>
        public static bool IsInHomeBoard(int nodeIndex, int playerId)
        {
            if (!IsOnBoard(nodeIndex))
                return false;

            // Player 0 moves forward -> home is 18..23, player 1 moves backward -> home is 0..5.
            return playerId == 0
                ? nodeIndex >= NodeCount - HomeBoardSize
                : nodeIndex < HomeBoardSize;
        }

        /// <summary>Distance this checker needs to leave the board (1..6 inside the home board).</summary>
        public static int GetBearOffDistance(int nodeIndex, int playerId)
        {
            return playerId == 0 ? NodeCount - nodeIndex : nodeIndex + 1;
        }

        /// <summary>Number of checkers the player has borne off so far.</summary>
        public int GetBorneOffCount(int playerId)
        {
            ThrowIfInvalidPlayer(playerId);
            return _borneOff[playerId];
        }

        /// <summary>
        /// Lets the View ask "can this checker be borne off?". Does not touch the board.
        /// </summary>
        public bool CanBearOff(int fromIndex)
        {
            return ResolveBearOff(fromIndex, CurrentPlayerId, false).Success;
        }

        /// <summary>
        /// Bear-off attempt. Does exactly the same as <c>TryMove(fromIndex, BearOffIndex)</c>;
        /// it has its own name so the intent reads clearly at the call site.
        /// </summary>
        public bool TryBearOff(int fromIndex)
        {
            return TryMove(fromIndex, BearOffIndex);
        }

        /// <summary>
        /// FREE BEAR-OFF — the classic "all checkers must be home" rule does NOT exist here.
        /// Only condition: the checker is in its own home board and a large enough die is available.
        /// Where the player's other checkers are does not matter.
        /// </summary>
        private MoveResult ResolveBearOff(int fromIndex, int playerId, bool commit)
        {
            if (!IsOnBoard(fromIndex))
                return MoveResult.Fail(MoveError.OutOfBounds, fromIndex, BearOffIndex, playerId);

            // The bar rule still applies: no bearing off before entering the checker on the bar.
            if (GetBarCount(playerId) > 0)
                return MoveResult.Fail(MoveError.MustEnterFromBar, fromIndex, BearOffIndex, playerId);

            BaseChecker mover = PeekSource(fromIndex, playerId);
            if (mover == null)
                return MoveResult.Fail(MoveError.SourceEmpty, fromIndex, BearOffIndex, playerId);

            if (mover.OwnerId != playerId)
                return MoveResult.Fail(MoveError.NotYourChecker, fromIndex, BearOffIndex, playerId);

            // The Swamp lock and the Dungeon bars block bearing off too.
            Node source = _nodes[fromIndex];
            if (source.ActiveTile != null && !source.ActiveTile.CanLeaveFrom(mover))
                return MoveResult.Fail(MoveError.CheckerLocked, fromIndex, BearOffIndex, playerId);

            if (!IsInHomeBoard(fromIndex, playerId))
                return MoveResult.Fail(MoveError.NotInHomeBoard, fromIndex, BearOffIndex, playerId);

            // "Die >= remaining distance" — no exact-match requirement as in classic backgammon.
            int remaining = GetBearOffDistance(fromIndex, playerId);
            int usedDie;
            if (!TryConsumeForBearOff(remaining, commit, out usedDie))
                return MoveResult.Fail(MoveError.NoDieForBearOff, fromIndex, BearOffIndex, playerId);

            // A dry query stops here — no die was spent either (commit=false).
            if (!commit)
                return MoveResult.BearOff(fromIndex, BearOffIndex, playerId, mover, 0);

            RemoveFromNode(source, mover);

            _borneOff[playerId]++;
            int scored = AwardScore(playerId, BearOffScore);

            // Two events, two responsibilities:
            //   CheckerBorneOff  -> game meaning (score burst, counter)
            //   CheckerDestroyed -> visual lifecycle (the View destroys the object)
            // Thanks to the second one, the checker disappears from the scene without any View changes.
            CheckerBorneOff?.Invoke(mover, fromIndex);
            CheckerDestroyed?.Invoke(mover, fromIndex);

            MoveResult result = MoveResult.BearOff(fromIndex, BearOffIndex, playerId, mover, scored);

            // Bearing off the last checker in play ends the match or starts the final stretch.
            HandleCheckerLeftPlay(playerId);

            return result;
        }

        /// <summary>
        /// Spends a die for bearing off. Picks the SMALLEST sufficient die so large dice are not wasted.
        /// With commit=false it only looks and spends nothing.
        /// </summary>
        private bool TryConsumeForBearOff(int remaining, bool commit, out int usedDie)
        {
            usedDie = 0;

            DiceRoll roll = Context != null ? Context.CurrentRoll : null;

            // Without a dice system, the classic 1-6 range is enough (test scenarios).
            if (roll == null)
            {
                usedDie = remaining;
                return remaining >= 1 && remaining <= MaxDieValue;
            }

            // With Cartographer's Cheat active, exactly the distance is taken from the pool.
            if (roll.FreeSplit)
            {
                if (!roll.CanAfford(remaining))
                    return false;

                usedDie = remaining;
                if (commit)
                    roll.Consume(remaining);
                return true;
            }

            int best = int.MaxValue;
            IReadOnlyList<int> moves = roll.RemainingMoves;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i] >= remaining && moves[i] < best)
                    best = moves[i];
            }

            if (best == int.MaxValue)
                return false;

            usedDie = best;
            if (commit)
                roll.Consume(best);

            return true;
        }
    }
}

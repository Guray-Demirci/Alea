using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
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
        /// Can the current player's topmost checker on this point be borne off? Does not touch the board.
        /// </summary>
        public bool CanBearOff(int fromIndex)
        {
            return ResolveBearOff(fromIndex, CurrentPlayerId, false, null).Success;
        }

        /// <summary>
        /// Lets the View ask "can THIS checker be borne off?", wherever it sits in its stack. Does not touch the board.
        /// </summary>
        public bool CanBearOff(BaseChecker checker)
        {
            return checker != null && ResolveBearOff(FindNodeIndexOf(checker), CurrentPlayerId, false, checker).Success;
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
        /// <param name="chosen">The checker to bear off; null for the topmost one on the point.</param>
        private MoveResult ResolveBearOff(int fromIndex, int playerId, bool commit, BaseChecker chosen)
        {
            if (!IsOnBoard(fromIndex))
                return MoveResult.Fail(MoveError.OutOfBounds, fromIndex, BearOffIndex, playerId);

            // The bar rule still applies: no bearing off before entering the checker on the bar.
            if (GetBarCount(playerId) > 0)
                return MoveResult.Fail(MoveError.MustEnterFromBar, fromIndex, BearOffIndex, playerId);

            BaseChecker mover = PeekSource(fromIndex, playerId, chosen);
            if (mover == null)
                return MoveResult.Fail(MoveError.SourceEmpty, fromIndex, BearOffIndex, playerId);

            if (mover.OwnerId != playerId)
                return MoveResult.Fail(MoveError.NotYourChecker, fromIndex, BearOffIndex, playerId);

            // The Swamp lock and the Dungeon bars block bearing off too.
            Node source = _nodes[fromIndex];
            ITileEffect sourceTile = TileFor(source, mover);
            if (sourceTile != null && !sourceTile.CanLeaveFrom(mover))
                return MoveResult.Fail(MoveError.CheckerLocked, fromIndex, BearOffIndex, playerId);

            if (!IsInHomeBoard(fromIndex, playerId))
                return MoveResult.Fail(MoveError.NotInHomeBoard, fromIndex, BearOffIndex, playerId);

            // "Die >= remaining distance" — no exact-match requirement as in classic backgammon.
            // The Nomad's bonus carries it off the board too, so a smaller die may be enough for it.
            int remaining = GetBearOffDistance(fromIndex, playerId);
            int usedDie;
            if (!TryConsumeForBearOff(mover, fromIndex, playerId, remaining, commit, out usedDie))
                return MoveResult.Fail(MoveError.NoDieForBearOff, fromIndex, BearOffIndex, playerId);

            // A dry query stops here — no die was spent either (commit=false).
            if (!commit)
                return MoveResult.BearOff(fromIndex, BearOffIndex, remaining, playerId, mover, 0);

            if (usedDie < remaining)
                ReportAbility(AbilityIds.ExtraDistance, mover, fromIndex);

            RemoveFromNode(source, mover);

            _borneOff[playerId]++;

            // The bear-off's score may reach the target, but the checker's own reaction (Golden Checker's gold) still
            // belongs to this bear-off: the run settles the purse the moment the match ends, so the end waits.
            _holdMatchEnd = true;
            MoveResult result;
            try
            {
                int scored = AwardScore(playerId, ScoreSource.BearOff, mover, fromIndex);

                // Two events, two responsibilities:
                //   CheckerBorneOff  -> game meaning (score burst, counter)
                //   CheckerDestroyed -> visual lifecycle (the View destroys the object)
                // Thanks to the second one, the checker disappears from the scene without any View changes.
                CheckerBorneOff?.Invoke(mover, fromIndex);
                CheckerDestroyed?.Invoke(mover, fromIndex);

                result = MoveResult.BearOff(fromIndex, BearOffIndex, remaining, playerId, mover, scored);

                // Before the match-end checks below, so a reward earned by the last checker still counts.
                NotifyBearOffReactor(mover, result);
            }
            finally
            {
                ReleaseMatchEnd();
            }

            // Bearing off the last checker in play ends the match or starts the final stretch.
            HandleCheckerLeftPlay(playerId);

            return result;
        }

        /// <summary>
        /// Spends a die for bearing off. Picks the SMALLEST die that carries the checker off so large dice are not
        /// wasted. With commit=false it only looks and spends nothing.
        /// </summary>
        private bool TryConsumeForBearOff(BaseChecker mover, int fromIndex, int playerId, int remaining, bool commit,
                                          out int usedDie)
        {
            usedDie = 0;

            IExtraDistanceProvider extra = mover.GetAbility<IExtraDistanceProvider>();
            DiceRoll roll = Context != null ? Context.CurrentRoll : null;

            // Without a dice system the classic 1-6 range is enough (test scenarios). With Cartographer's Cheat
            // active, exactly the die's value is taken from the pool.
            if (roll == null || roll.FreeSplit)
            {
                for (int die = 1; die <= remaining && die <= MaxDieValue; die++)
                {
                    if (roll != null && !roll.CanAfford(die))
                        return false; // A larger value is out of the pool too.

                    if (!CarriesOff(extra, fromIndex, playerId, remaining, die))
                        continue;

                    usedDie = die;
                    if (commit && roll != null)
                        roll.Consume(die);
                    return true;
                }

                return false;
            }

            int best = int.MaxValue;
            IReadOnlyList<int> moves = roll.RemainingMoves;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i] < best && CarriesOff(extra, fromIndex, playerId, remaining, moves[i]))
                    best = moves[i];
            }

            if (best == int.MaxValue)
                return false;

            usedDie = best;
            if (commit)
                roll.Consume(best);

            return true;
        }

        /// <summary>
        /// Does this die take the checker off the board — on its own, or stretched by the Nomad's bonus earned on
        /// the point the die brings it to?
        /// </summary>
        private bool CarriesOff(IExtraDistanceProvider extra, int fromIndex, int playerId, int remaining, int die)
        {
            if (die >= remaining)
                return true;

            return extra != null &&
                   die + extra.GetBonusDistance(this, fromIndex + die * GetDirection(playerId)) >= remaining;
        }
    }
}

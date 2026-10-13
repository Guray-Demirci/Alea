using System;
using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Board
{
    // Dice bridge, turn flow and the end of the match.
    public partial class GameState
    {
        #region Dice bridge

        // If no dice system is bound yet (Context == null) we fall back to classic 1-6 validation,
        // so the architecture stays testable without the dice system.

        private bool CanAffordDistance(int distance)
        {
            if (distance <= 0)
                return false;

            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            if (roll == null)
                return distance <= MaxDieValue;

            return roll.CanAfford(distance);
        }

        private void ConsumeDistance(int distance)
        {
            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            if (roll != null)
                roll.Consume(distance);
        }

        /// <summary>Is any die left to pay with? Without a dice system there always is.</summary>
        private bool HasDiceLeft()
        {
            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            return roll == null || roll.HasMovesLeft;
        }

        /// <summary>Teleporter Checker: its free move costs every die left.</summary>
        private void ConsumeAllDice()
        {
            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            if (roll != null)
                roll.ConsumeAll();
        }

        /// <summary>
        /// MatchContext reports every finished roll here. The rolling player's checkers on the board react to it
        /// (Kraken). They are collected first: a reaction may capture checkers and reshape the points being scanned.
        /// A simulation context never rolls, so the bot's look-ahead cannot trigger this.
        /// </summary>
        internal void NotifyDiceRolled(int playerId, DiceRoll roll)
        {
            if (!SideEffectsEnabled || IsMatchOver)
                return;

            List<BaseChecker> reacting = null;
            for (int i = 0; i < NodeCount; i++)
            {
                Node node = _nodes[i];
                for (int c = 0; c < node.CheckerCount; c++)
                {
                    BaseChecker checker = node.Checkers[c];
                    if (checker.OwnerId != playerId || checker.GetAbility<IRollReactor>() == null)
                        continue;

                    if (reacting == null)
                        reacting = new List<BaseChecker>();

                    reacting.Add(checker);
                }
            }

            if (reacting == null)
                return;

            for (int i = 0; i < reacting.Count && !IsMatchOver; i++)
            {
                // An earlier reaction may have taken this checker off the board or into the Fog.
                IRollReactor reactor = reacting[i].GetAbility<IRollReactor>();
                if (reactor != null && IsOnBoard(FindNodeIndexOf(reacting[i])))
                    reactor.OnDiceRolled(Context, roll);
            }
        }

        #endregion

        #region Turn flow

        /// <summary>Triggered by the Time Bender Checker.</summary>
        internal void RequestSkipTurn(int playerId)
        {
            if (IsValidPlayer(playerId))
                _skipNextTurn[playerId] = true;
        }

        /// <summary>
        /// Ends the turn: ticks tile counters, counts the turn and passes it on. A volley still waiting for its
        /// targets (Bowman) is lost.
        /// </summary>
        public void EndTurn()
        {
            DropVolley();

            // Once the match is closed, turns no longer advance either.
            if (IsMatchOver)
                return;

            int endingPlayerId = CurrentPlayerId;
            NotifyTiles((tile, node) => tile.OnTurnPassed(Context, node, endingPlayerId));

            // A tile above may have ended the match (a Powder Keg destroying the last checker).
            // The turn must not be counted or passed on after that.
            if (IsMatchOver)
                return;

            // TIME BOMB: the counter drops at the end of every turn the rules count
            // (every turn in Classic mode, only the scoring player's turns with a target score).
            if (_rules.CountsTurnOf(CurrentPlayerId))
            {
                RemainingTurns--;
                TurnCountChanged?.Invoke(RemainingTurns);

                if (RemainingTurns <= 0)
                {
                    FinishMatch();
                    return; // The turn is not passed on; the match is over.
                }
            }

            AdvanceTurn();
        }

        /// <summary>
        /// MatchContext reports every score change here — it is the one place ALL score passes through,
        /// both the rule engine's (captures, bear-off) and the abilities' own AddScore calls.
        /// Ends the match the moment the scoring player reaches the target.
        /// A simulation context never calls this, so the bot's look-ahead cannot end the real match.
        /// While a bear-off holds the end, reaching the target is only noted; <see cref="ReleaseMatchEnd"/> acts on it.
        /// </summary>
        internal void NotifyScoreChanged(int playerId, int totalScore)
        {
            if (IsMatchOver || !_rules.IsTargetReached(playerId, totalScore))
                return;

            if (_holdMatchEnd)
                _matchEndHeld = true;
            else
                FinishMatch();
        }

        /// <summary>Lifts a bear-off's hold and ends the match if the target was reached while it was held.</summary>
        private void ReleaseMatchEnd()
        {
            _holdMatchEnd = false;
            if (!_matchEndHeld)
                return;

            _matchEndHeld = false;
            FinishMatch();
        }

        /// <summary>
        /// Called whenever a checker leaves play for good (borne off or destroyed). If that was the owner's
        /// last checker, the rules decide: the match ends, or — for the scoring player's opponent under a
        /// target score — the final stretch begins.
        /// </summary>
        private void HandleCheckerLeftPlay(int ownerId)
        {
            if (IsMatchOver || _outOfCheckers[ownerId] || HasCheckersInPlay(ownerId))
                return;

            _outOfCheckers[ownerId] = true;

            if (_rules.StartsFinalStretch(ownerId))
                StartFinalStretch();
            else
                FinishMatch();
        }

        /// <summary>
        /// The opponent has nothing left to move: the scoring player plays on alone, with the remaining turns
        /// capped. The cap only ever lowers the counter — a player already short on turns is not rewarded.
        /// </summary>
        private void StartFinalStretch()
        {
            IsInFinalStretch = true;

            if (RemainingTurns > _rules.FinalStretchTurns)
            {
                RemainingTurns = _rules.FinalStretchTurns;
                TurnCountChanged?.Invoke(RemainingTurns);
            }

            FinalStretchStarted?.Invoke(RemainingTurns);
        }

        /// <summary>
        /// Closes the match and declares the winner. Called when the turn counter runs out, when a player
        /// runs out of checkers (unless the final stretch begins instead) and when the target score is reached.
        /// </summary>
        private void FinishMatch()
        {
            if (IsMatchOver)
                return;

            IsMatchOver = true;
            WinnerId = DetermineWinner();
            MatchEnded?.Invoke(WinnerId);
        }

        /// <summary>
        /// With a target score: the scoring player wins if the target is reached, otherwise the opponent wins —
        /// there are no draws. Classic mode: the player with the higher score wins.
        /// MatchContext is the single source of the score, so the decision is made by reading its values
        /// rather than keeping a copy here.
        /// </summary>
        private int DetermineWinner()
        {
            if (Context == null)
                return NoWinner;

            if (_rules.HasTargetScore)
            {
                int scorer = _rules.ScoringPlayerId;
                return _rules.IsTargetReached(scorer, Context.GetScore(scorer)) ? scorer : GetOpponent(scorer);
            }

            int bestPlayer = NoWinner;
            int bestScore = int.MinValue;
            bool tied = false;

            for (int p = 0; p < PlayerCount; p++)
            {
                int score = Context.GetScore(p);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPlayer = p;
                    tied = false;
                }
                else if (score == bestScore)
                {
                    tied = true;
                }
            }

            return tied ? NoWinner : bestPlayer;
        }

        private void AdvanceTurn()
        {
            _playedThisRound[CurrentPlayerId] = true;

            int next = (CurrentPlayerId + 1) % PlayerCount;

            if (_skipNextTurn[next])
            {
                _skipNextTurn[next] = false;
                next = (next + 1) % PlayerCount; // Turn skipped, it returns to the same player.
            }

            // A player with no checkers left has no turns either; the other player keeps playing.
            // A skip aimed at that other player is consumed without effect — there is nobody to pass to.
            if (_outOfCheckers[next])
                next = (next + 1) % PlayerCount;

            // A round is over once the turn comes back to a player who has already played in it: normally
            // after both players, but also when a skip or the final stretch hands one player the next turn.
            if (_playedThisRound[next])
            {
                CompleteRound();

                // A round hook may have ended the match; then the turn is not passed on.
                if (IsMatchOver)
                    return;
            }

            CurrentPlayerId = next;
            TurnChanged?.Invoke(CurrentPlayerId);
        }

        private void CompleteRound()
        {
            Array.Clear(_playedThisRound, 0, _playedThisRound.Length);
            NotifyTiles((tile, node) => tile.OnRoundPassed(Context, node));
        }

        /// <summary>
        /// Runs a turn-flow hook on every tile, then removes the tiles that expired because of it.
        /// Skipped in simulations: tile objects are shared with the real board.
        /// </summary>
        private void NotifyTiles(Action<ITileEffect, Node> hook)
        {
            if (!SideEffectsEnabled)
                return;

            for (int i = 0; i < NodeCount; i++)
            {
                Node node = _nodes[i];
                ITileEffect tile = node.ActiveTile;
                if (tile == null)
                    continue;

                hook(tile, node);

                // The hook may already have cleared the tile through a checker leaving the point.
                if (tile.IsExpired && node.ActiveTile == tile)
                    node.ClearTile();
            }
        }

        #endregion
    }
}

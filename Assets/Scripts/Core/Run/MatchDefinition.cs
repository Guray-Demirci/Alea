using System;
using Tavla.Core.Board;

namespace Tavla.Core.Run
{
    /// <summary>
    /// One match of a run as the designer describes it: the score to reach, the turns to reach it in and the
    /// opponent's strength. Immutable and validated on construction.
    ///
    /// The player's side is NOT here: the player brings their deck (<see cref="RunState.Deck"/>), which only grows
    /// through the shop. <see cref="RunState"/> turns this entry into the <see cref="MatchRules"/> GameState plays by.
    /// </summary>
    public sealed class MatchDefinition
    {
        /// <summary>Score the player must reach to win the match.</summary>
        public int TargetScore { get; }

        /// <summary>The player's turns in which the target must be reached. The opponent's turns are not counted.</summary>
        public int TurnLimit { get; }

        /// <summary>Checkers the opponent starts with, dealt by <see cref="StartingLayout"/>.</summary>
        public int OpponentCheckers { get; }

        /// <summary>A boss (challenge) match: the last match of a stage, and it pays <see cref="RunEconomy.BossWinGold"/>.</summary>
        public bool IsBoss { get; }

        /// <exception cref="ArgumentOutOfRangeException">Any value outside its range.</exception>
        public MatchDefinition(int targetScore, int turnLimit, int opponentCheckers, bool isBoss = false)
        {
            if (targetScore < 1)
                throw new ArgumentOutOfRangeException(nameof(targetScore), targetScore, "The target score must be positive.");

            if (turnLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(turnLimit), turnLimit, "The turn limit must be positive.");

            if (opponentCheckers < GameState.MinCheckersPerPlayer || opponentCheckers > GameState.MaxCheckersPerPlayer)
                throw new ArgumentOutOfRangeException(nameof(opponentCheckers), opponentCheckers,
                    $"A side starts with {GameState.MinCheckersPerPlayer}..{GameState.MaxCheckersPerPlayer} checkers.");

            TargetScore = targetScore;
            TurnLimit = turnLimit;
            OpponentCheckers = opponentCheckers;
            IsBoss = isBoss;
        }
    }
}

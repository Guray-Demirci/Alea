using System;

namespace Tavla.Core.Board
{
    /// <summary>
    /// How a match is won and how long it lasts. Immutable, so a cloned board can share the same instance.
    ///
    /// Two modes:
    ///   Classic : every turn of both players counts; when the turns run out, or a player runs out of
    ///             checkers, the higher score wins. The sandbox scene and most tests use this mode.
    ///   Target  : one player chases a target score. Only that player's turns are counted, the match ends
    ///             the moment the target is reached, and missing it means losing. When the opponent runs
    ///             out of checkers the match does not end — the final stretch begins instead.
    ///
    /// GameState reads these answers instead of branching on game modes itself, so a new mode means a new
    /// factory method here rather than new conditions scattered through the rules.
    /// </summary>
    public sealed class MatchRules
    {
        /// <summary>Turns available in the match. Which turns count is decided by <see cref="CountsTurnOf"/>.</summary>
        public int TurnLimit { get; }

        /// <summary>Score the scoring player must reach; null in Classic mode.</summary>
        public int? TargetScore { get; }

        /// <summary>The player who chases the target and whose turns are counted. Only meaningful with a target.</summary>
        public int ScoringPlayerId { get; }

        /// <summary>
        /// Remaining-turn cap applied when the opponent runs out of checkers. 0 means the match simply ends.
        /// Only meaningful with a target.
        /// </summary>
        public int FinalStretchTurns { get; }

        public bool HasTargetScore => TargetScore.HasValue;

        private MatchRules(int turnLimit, int? targetScore, int scoringPlayerId, int finalStretchTurns)
        {
            TurnLimit = turnLimit;
            TargetScore = targetScore;
            ScoringPlayerId = scoringPlayerId;
            FinalStretchTurns = finalStretchTurns;
        }

        /// <summary>Higher score wins; every turn of both players counts.</summary>
        /// <param name="turnLimit">Values below 1 are raised to 1, as GameState has always done.</param>
        public static MatchRules Classic(int turnLimit = GameState.DefaultTurnLimit)
        {
            return new MatchRules(Math.Max(1, turnLimit), null, 0, 0);
        }

        /// <summary>The scoring player must reach <paramref name="targetScore"/> within their own turns.</summary>
        public static MatchRules ForTarget(int targetScore, int turnLimit, int finalStretchTurns, int scoringPlayerId = 0)
        {
            if (targetScore < 1)
                throw new ArgumentOutOfRangeException(nameof(targetScore), targetScore, "The target score must be positive.");

            if (turnLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(turnLimit), turnLimit, "The turn limit must be positive.");

            if (finalStretchTurns < 0)
                throw new ArgumentOutOfRangeException(nameof(finalStretchTurns), finalStretchTurns,
                    "The final stretch cannot be negative; use 0 to end the match instead.");

            if (!GameState.IsValidPlayer(scoringPlayerId))
                throw new ArgumentOutOfRangeException(nameof(scoringPlayerId), scoringPlayerId,
                    $"Player id must be in the range 0..{GameState.PlayerCount - 1}.");

            return new MatchRules(turnLimit, targetScore, scoringPlayerId, finalStretchTurns);
        }

        /// <summary>Does the turn counter drop when this player ends a turn?</summary>
        public bool CountsTurnOf(int playerId) => !HasTargetScore || playerId == ScoringPlayerId;

        /// <summary>Has this player reached the target with the given score? Always false in Classic mode.</summary>
        public bool IsTargetReached(int playerId, int score)
        {
            return HasTargetScore && playerId == ScoringPlayerId && score >= TargetScore.Value;
        }

        /// <summary>
        /// When this player runs out of checkers, does the match go on as a final stretch instead of ending?
        /// Only the scoring player's opponent can trigger it, and only when a stretch is configured.
        /// </summary>
        public bool StartsFinalStretch(int playerOutOfCheckers)
        {
            return HasTargetScore && playerOutOfCheckers != ScoringPlayerId && FinalStretchTurns > 0;
        }
    }
}

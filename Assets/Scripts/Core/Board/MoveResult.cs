using System;
using System.Collections.Generic;
using Tavla.Core.Checkers;

namespace Tavla.Core.Board
{
    /// <summary>Reason code explaining why a move was rejected. The View turns it into a UI message.</summary>
    public enum MoveError
    {
        None = 0,
        OutOfBounds,        // Point index is outside the board
        SourceEmpty,        // Source point is empty
        NotYourChecker,     // Checker on the source belongs to the opponent
        WrongDirection,     // Move goes against the player's direction of travel
        InvalidDistance,    // Distance is outside the 1-6 range
        NoDiceValue,        // No die covers this distance
        DestinationBlocked, // Destination is held by the opponent
        StackFull,          // Point capacity reached (GameState.StackLimit)
        BlockedByTile,      // A special tile on the destination blocked entry (Dungeon)
        CheckerLocked,      // Checker is locked on its point (Swamp / Dungeon)
        MustEnterFromBar,   // A normal move is not allowed while a checker is on the bar

        // --- Arcade mode ---
        MatchOver,          // Turn counter ran out, the match is closed
        NotInHomeBoard,     // Bear-off attempted from outside the home board
        NoDieForBearOff     // No die is large enough to cover the remaining distance
    }

    /// <summary>
    /// Result of a move attempt.
    /// The Doomsday Checker can capture several checkers in one move, so captures are stored as a LIST.
    ///
    /// ARCADE MODE: also carries score and bear-off information. The View uses these two fields
    /// to play the score animation and to remove the borne-off checker from the scene.
    /// </summary>
    public readonly struct MoveResult
    {
        public readonly bool Success;
        public readonly MoveError Error;
        public readonly int FromIndex;
        public readonly int ToIndex;
        public readonly int PlayerId;

        /// <summary>
        /// Points the checker travelled, measured from its real origin — the bar origin when it entered
        /// from the bar, which <see cref="FromIndex"/> (<see cref="GameState.BarIndex"/>) cannot express.
        /// Includes the Nomad Checker's bonus. For a bear-off it is the distance that was left to the edge
        /// of the board. 0 for a failed move.
        /// </summary>
        public readonly int Distance;

        /// <summary>The checker that moved (may be null for a failed move).</summary>
        public readonly BaseChecker MovedChecker;

        /// <summary>Opponent checkers captured by this move. Empty (never null) if nothing was captured.</summary>
        public readonly IReadOnlyList<BaseChecker> CapturedCheckers;

        /// <summary>True if a Parasite Checker infiltrated an enemy point — the View plays a different animation.</summary>
        public readonly bool WasInfiltration;

        /// <summary>
        /// Was this a BEAR-OFF? If true the checker left the board completely and the View
        /// must destroy its GameObject. <see cref="ToIndex"/> is <see cref="GameState.BearOffIndex"/> in that case.
        /// </summary>
        public readonly bool WasBearOff;

        /// <summary>
        /// Score this move earned for the player — the value actually written to the context.
        /// It is the sum of capture and bear-off points. For the authoritative total listen to
        /// <c>MatchContext.ScoreChanged</c>; this value only answers "what did this move earn?".
        /// </summary>
        public readonly int ScoreGained;

        public bool HasCapture => CapturedCheckers.Count > 0;

        private MoveResult(bool success, MoveError error, int from, int to, int distance, int playerId,
                           BaseChecker moved, IReadOnlyList<BaseChecker> captured,
                           bool infiltration, bool bearOff, int scoreGained)
        {
            Success = success;
            Error = error;
            FromIndex = from;
            ToIndex = to;
            Distance = distance;
            PlayerId = playerId;
            MovedChecker = moved;
            CapturedCheckers = captured ?? Array.Empty<BaseChecker>();
            WasInfiltration = infiltration;
            WasBearOff = bearOff;
            ScoreGained = scoreGained;
        }

        /// <summary>Successful move on the board.</summary>
        public static MoveResult Ok(int from, int to, int distance, int playerId, BaseChecker moved,
                                    IReadOnlyList<BaseChecker> captured, bool infiltration,
                                    int scoreGained)
            => new MoveResult(true, MoveError.None, from, to, distance, playerId, moved, captured,
                              infiltration, false, scoreGained);

        /// <summary>
        /// Successful bear-off. The destination is not a point, so <see cref="ToIndex"/> holds a
        /// sentinel value and the capture list is always empty.
        /// </summary>
        public static MoveResult BearOff(int from, int toSentinel, int distance, int playerId,
                                         BaseChecker moved, int scoreGained)
            => new MoveResult(true, MoveError.None, from, toSentinel, distance, playerId, moved, null,
                              false, true, scoreGained);

        public static MoveResult Fail(MoveError error, int from, int to, int playerId)
            => new MoveResult(false, error, from, to, 0, playerId, null, null, false, false, 0);
    }
}

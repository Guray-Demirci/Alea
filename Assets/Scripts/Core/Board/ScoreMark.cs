namespace Tavla.Core.Board
{
    /// <summary>
    /// A player's mark on a point that sets the BASE score they earn there — the Navigator Checker's footprints.
    /// Any of the player's checkers earns it, and the earner's own modifier (Catalyst) still applies on top.
    /// A mark stays for the rest of the match, whatever happens to the checker that left it.
    /// Immutable, so one instance can mark any number of points.
    /// </summary>
    public sealed class ScoreMark
    {
        /// <summary>Base score of a capture made on the marked point, instead of <see cref="GameState.CaptureScore"/>.</summary>
        public int CaptureScore { get; }

        /// <summary>Base score of a bear-off from the marked point, instead of <see cref="GameState.BearOffScore"/>.</summary>
        public int BearOffScore { get; }

        public ScoreMark(int captureScore, int bearOffScore)
        {
            CaptureScore = captureScore;
            BearOffScore = bearOffScore;
        }
    }
}

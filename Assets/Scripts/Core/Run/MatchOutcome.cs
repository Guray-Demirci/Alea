namespace Tavla.Core.Run
{
    /// <summary>How a match of a run ended and what it paid. Raised by <see cref="RunState.MatchFinished"/>.</summary>
    public sealed class MatchOutcome
    {
        public int StageIndex { get; }

        /// <summary>Index of the match within its stage.</summary>
        public int MatchIndex { get; }

        public bool IsWin { get; }

        /// <summary>The player's final score in the match.</summary>
        public int Score { get; }

        public int TargetScore { get; }

        /// <summary>Turns of the limit left over on a win; 0 on a loss.</summary>
        public int UnusedTurns { get; }

        /// <summary>The match's win reward; 0 on a loss.</summary>
        public int WinGold { get; }

        /// <summary>The bonus for <see cref="UnusedTurns"/>; 0 on a loss.</summary>
        public int UnusedTurnGold { get; }

        public int RewardGold => WinGold + UnusedTurnGold;

        /// <summary>The player's gold once the reward is paid — what they carry to the next match.</summary>
        public int GoldAfter { get; }

        internal MatchOutcome(int stageIndex, int matchIndex, bool isWin, int score, int targetScore,
                              int unusedTurns, int winGold, int unusedTurnGold, int goldAfter)
        {
            StageIndex = stageIndex;
            MatchIndex = matchIndex;
            IsWin = isWin;
            Score = score;
            TargetScore = targetScore;
            UnusedTurns = unusedTurns;
            WinGold = winGold;
            UnusedTurnGold = unusedTurnGold;
            GoldAfter = goldAfter;
        }
    }
}

using Tavla.Core.Board;

namespace Tavla.Core.AI
{
    /// <summary>
    /// A single candidate move evaluated by the bot.
    /// When <see cref="ToIndex"/> equals <see cref="GameState.BearOffIndex"/> the move is a bear-off.
    /// </summary>
    public readonly struct BotMove
    {
        public readonly bool IsValid;
        public readonly int FromIndex;
        public readonly int ToIndex;

        /// <summary>Heuristic score of the board after this move is applied.</summary>
        public readonly int Score;

        public bool IsBearOff => ToIndex == GameState.BearOffIndex;

        public BotMove(int fromIndex, int toIndex, int score)
        {
            IsValid = true;
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Score = score;
        }

        /// <summary>No playable move was found.</summary>
        public static BotMove None => default(BotMove);
    }
}

using Tavla.Core.Board;

namespace Tavla.Core.AI
{
    /// <summary>
    /// A single candidate action evaluated by the bot.
    /// When <see cref="ToIndex"/> equals <see cref="GameState.BearOffIndex"/> the move is a bear-off.
    /// When <see cref="IsRelease"/> is set it is not a move at all: a die is sacrificed to free the
    /// locked checker on <see cref="FromIndex"/>, which stays where it is.
    /// </summary>
    public readonly struct BotMove
    {
        public readonly bool IsValid;
        public readonly int FromIndex;
        public readonly int ToIndex;

        /// <summary>Frees the locked checker on <see cref="FromIndex"/> (Swamp); <see cref="ToIndex"/> equals it.</summary>
        public readonly bool IsRelease;

        /// <summary>Heuristic score of the board after this move is applied.</summary>
        public readonly int Score;

        public bool IsBearOff => !IsRelease && ToIndex == GameState.BearOffIndex;

        public BotMove(int fromIndex, int toIndex, int score) : this(fromIndex, toIndex, false, score)
        {
        }

        private BotMove(int fromIndex, int toIndex, bool isRelease, int score)
        {
            IsValid = true;
            FromIndex = fromIndex;
            ToIndex = toIndex;
            IsRelease = isRelease;
            Score = score;
        }

        /// <summary>Sacrifice a die to free the locked checker on the given point.</summary>
        public static BotMove Release(int nodeIndex, int score) => new BotMove(nodeIndex, nodeIndex, true, score);

        /// <summary>No playable move was found.</summary>
        public static BotMove None => default(BotMove);
    }
}

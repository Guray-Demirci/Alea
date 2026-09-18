using Tavla.Core.Board;

namespace Tavla.Core.AI
{
    /// <summary>
    /// Botun degerlendirdigi tek bir aday hamle.
    /// ToIndex == GameState.BearOffIndex ise bu bir toplama hamlesidir.
    /// </summary>
    public readonly struct BotMove
    {
        public readonly bool IsValid;
        public readonly int FromIndex;
        public readonly int ToIndex;

        /// <summary>Bu hamle uygulandiktan sonra tahtanin aldigi sezgisel puan.</summary>
        public readonly int Score;

        public bool IsBearOff => ToIndex == GameState.BearOffIndex;

        public BotMove(int fromIndex, int toIndex, int score)
        {
            IsValid = true;
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Score = score;
        }

        /// <summary>Oynanacak hamle bulunamadi.</summary>
        public static BotMove None => default(BotMove);
    }
}

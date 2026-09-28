using System;

namespace Tavla.Core.Board
{
    /// <summary>
    /// Where a player's checkers start, for any count from 1 to <see cref="GameState.MaxCheckersPerPlayer"/>.
    ///
    /// The layout is a FILL ORDER: the k-th checker always goes to the k-th slot. Adding a checker therefore
    /// never moves the ones before it — an opponent that grows by one checker per level keeps its formation,
    /// and a deck keeps its special checkers on the same points. The full 15 slots form the classic
    /// 2-5-3-5 formation, so the standard board is simply the full-length case.
    ///
    /// No point ever receives more than <see cref="GameState.DefaultStackLimit"/> checkers.
    /// Points are written from player 0's perspective and mirrored for player 1.
    /// </summary>
    public static class StartingLayout
    {
        //   count  5 : 11x2  16x1  18x2
        //   count 10 : 0x2  11x3  16x2  18x3
        //   count 15 : 0x2  11x5  16x3  18x5  (classic)
        private static readonly int[] FillOrder = { 11, 18, 16, 11, 18, 16, 18, 11, 0, 0, 11, 18, 16, 11, 18 };

        /// <summary>Number of slots; equals <see cref="GameState.MaxCheckersPerPlayer"/>.</summary>
        public static int SlotCount => FillOrder.Length;

        /// <summary>The point the checker in the given slot starts on.</summary>
        public static int GetPoint(int playerId, int slot)
        {
            if (!GameState.IsValidPlayer(playerId))
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId,
                    $"Player id must be in the range 0..{GameState.PlayerCount - 1}.");

            if (slot < 0 || slot >= FillOrder.Length)
                throw new ArgumentOutOfRangeException(nameof(slot), slot,
                    $"Slot must be in the range 0..{FillOrder.Length - 1}.");

            int point = FillOrder[slot];
            return playerId == 0 ? point : GameState.NodeCount - 1 - point;
        }
    }
}

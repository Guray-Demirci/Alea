using System;

namespace Tavla.Core.Run
{
    /// <summary>
    /// The gold rules of a run (GDD "Oyun Ekonomisi"): what the player starts with and what a win pays.
    /// Immutable and validated on construction. Shop prices are not here — they belong to the shop.
    /// </summary>
    public sealed class RunEconomy
    {
        /// <summary>Gold the player holds when the run begins.</summary>
        public int StartingGold { get; }

        /// <summary>Gold paid for winning a normal match.</summary>
        public int WinGold { get; }

        /// <summary>Gold paid for winning a boss (challenge) match.</summary>
        public int BossWinGold { get; }

        /// <summary>
        /// Gold paid on a win for every turn of the limit the player did not need.
        /// The turn in which the target is reached counts as used.
        /// </summary>
        public int GoldPerUnusedTurn { get; }

        /// <exception cref="ArgumentOutOfRangeException">A negative value.</exception>
        public RunEconomy(int startingGold, int winGold, int bossWinGold, int goldPerUnusedTurn)
        {
            ThrowIfNegative(startingGold, nameof(startingGold));
            ThrowIfNegative(winGold, nameof(winGold));
            ThrowIfNegative(bossWinGold, nameof(bossWinGold));
            ThrowIfNegative(goldPerUnusedTurn, nameof(goldPerUnusedTurn));

            StartingGold = startingGold;
            WinGold = winGold;
            BossWinGold = bossWinGold;
            GoldPerUnusedTurn = goldPerUnusedTurn;
        }

        /// <summary>What winning the given match pays, before the unused-turn bonus.</summary>
        public int GetWinGold(MatchDefinition match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            return match.IsBoss ? BossWinGold : WinGold;
        }

        private static void ThrowIfNegative(int value, string paramName)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(paramName, value, "Gold values cannot be negative.");
        }
    }
}

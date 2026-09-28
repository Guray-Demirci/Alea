namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// Describes items the player already owns when a run STARTS.
    /// Kept separate from <see cref="IPurchasable"/>: an item may be purchasable but not granted
    /// at the start (or the other way around). Thanks to this split (ISP) classes implement only what they need.
    /// </summary>
    public interface IStarterItem
    {
        /// <summary>How many copies the player receives at the start of a match.</summary>
        int StartingAmount { get; }

        /// <summary>Is it available for free, or must it be unlocked in the shop first?</summary>
        bool IsDefaultUnlocked { get; }
    }
}

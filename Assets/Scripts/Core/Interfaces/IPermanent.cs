namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// PURCHASABLE — PERMANENT BRANCH.
    /// Bought once and kept for the whole match (or permanently): special checkers,
    /// permanent dice relics, board skins.
    ///
    /// Mutually exclusive with <see cref="IConsumable"/>. An item must implement exactly ONE of them;
    /// an inventory system that sees a type implementing only <see cref="IPurchasable"/> may treat it as an error.
    /// </summary>
    public interface IPermanent : IPurchasable
    {
        /// <summary>
        /// Can the player own more than one of this item?
        /// Yes for checkers (you can buy 3 Ghost Checkers for your deck),
        /// no for dice relics (a second Twin Winds does nothing).
        /// The shop uses this flag to lock the product as "Owned".
        /// </summary>
        bool AllowsDuplicates { get; }
    }
}

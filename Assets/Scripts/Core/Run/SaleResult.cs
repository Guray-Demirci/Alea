namespace Tavla.Core.Run
{
    /// <summary>Whether a checker of the deck can be sold, and if not, why. <see cref="Shop.CheckSale"/> and <see cref="Shop.Sell"/> return it.</summary>
    public enum SaleResult
    {
        Success,
        ShopClosed,

        /// <summary>The checker is not (or no longer) in the deck.</summary>
        NotInDeck,

        /// <summary>
        /// A plain checker, but the deck has none to spare: only plain checkers beyond the ones the run started with
        /// are bought back (<see cref="Shop.ExtraPlainCheckerCount"/>).
        /// </summary>
        NotForSale
    }
}

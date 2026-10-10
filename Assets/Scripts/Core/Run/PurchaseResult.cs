namespace Tavla.Core.Run
{
    /// <summary>Whether a shelf can be bought, and if not, why. <see cref="Shop.CheckPurchase"/> and <see cref="Shop.Buy"/> return it.</summary>
    public enum PurchaseResult
    {
        Success,
        ShopClosed,

        /// <summary>Nothing is on the shelf: the catalog had nothing left to offer there.</summary>
        EmptySlot,

        AlreadySold,
        NotEnoughGold,

        /// <summary>The deck already holds as many checkers as a side can field.</summary>
        DeckFull,

        /// <summary>The deck already holds the most special checkers allowed (<see cref="ShopCatalog.MaxSpecialCheckers"/>).</summary>
        TooManySpecialCheckers,

        /// <summary>
        /// The run already owns this item: charms do nothing twice, and a deck holds one of each special checker.
        /// </summary>
        AlreadyOwned
    }
}

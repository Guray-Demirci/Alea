namespace Tavla.Core.Run
{
    /// <summary>An item on a shelf of one shop visit, at the price this visit asks for it.</summary>
    public sealed class ShopOffer
    {
        public ShopItem Item { get; }

        /// <summary>Final price: the base price after the Sacrificial Checker discount.</summary>
        public int Price { get; }

        public bool IsSold { get; private set; }

        internal ShopOffer(ShopItem item, int price)
        {
            Item = item;
            Price = price;
        }

        internal void MarkSold()
        {
            IsSold = true;
        }
    }
}

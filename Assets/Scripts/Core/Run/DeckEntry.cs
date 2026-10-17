using Tavla.Core.Checkers;

namespace Tavla.Core.Run
{
    /// <summary>
    /// One checker of the player's deck: what it is, what it cost, and how to make a fresh one for every match.
    /// Created by <see cref="RunState.AddToDeck"/>.
    /// </summary>
    public sealed class DeckEntry
    {
        private readonly CheckerFactory _create;

        /// <summary>The checker's <see cref="BaseChecker.TypeId"/>.</summary>
        public string TypeId { get; }

        /// <summary>Anything but a default checker. The shop limits how many of these a deck may hold.</summary>
        public bool IsSpecial => TypeId != DefaultChecker.Id;

        /// <summary>
        /// The gold paid for it in the shop, discounts included; what it sells back for is worked out from this
        /// (<see cref="ShopPrices.GetSellPrice"/>). 0 for a checker the run gave for free.
        /// </summary>
        public int PurchasePrice { get; }

        internal DeckEntry(string typeId, CheckerFactory create, int purchasePrice = 0)
        {
            TypeId = typeId;
            _create = create;
            PurchasePrice = purchasePrice;
        }

        internal BaseChecker Create(int ownerId) => _create(ownerId);
    }
}

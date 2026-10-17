using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Run
{
    /// <summary>
    /// One visit to the shop between two matches of a run. Opened by <see cref="RunState.OpenShop"/>, closed by
    /// <see cref="Close"/> ("NEXT"); the next match can only start once it is closed.
    ///
    /// Shelves (<see cref="ShopSlot"/>):
    ///   - three special checkers, drawn at random from the catalog, never the same one twice in a visit's stock
    ///     and never one the deck already holds (a deck holds one of each special checker). With the stage's
    ///     <see cref="ShopTierOdds"/> each shelf first rolls a tier, then draws evenly within it; a rolled tier
    ///     with nothing left gives way to the nearest one below it, then above it, among the tiers the odds allow.
    ///     Without odds every special checker is equally likely;
    ///   - the plain checker, always there and never sold out: it can be bought as long as the gold lasts and
    ///     the deck has room;
    ///   - one card and one charm, at random; a charm the run already owns is never offered.
    /// A reroll ("RAN") restocks every shelf except the plain checker. Each visit allows as many rerolls as the
    /// price list has reroll prices (1, 3, 5, 7, 9 gold), starting again from the first price next visit.
    ///
    /// The shop also buys back checkers of the deck (GDD: "elinde bulunan taşları satarak yerine başkalarını
    /// alabilecek") for half of what was paid for them: every special checker, which frees a place under the
    /// special checker limit, and the EXTRA plain checkers — those beyond the <see cref="RunDefinition.StartingCheckers"/>
    /// the run started with. The plain checkers are alike, so the dearest one goes first.
    ///
    /// Checker effects on the shop (GDD):
    ///   - Phoenix Checker ("Küllerinden Doğuş"): the next visit is a LUXURY shop — the special checker shelves
    ///     skip tier 1, at their normal prices, and the other tiers share its chance;
    ///   - Sacrificial Checker: every item costs <see cref="RunState.ShopDiscount"/> gold less, for the rest of
    ///     the run. Prices never go below 0; rerolls are not discounted.
    ///
    /// All randomness comes from the run's provider, so the run's seed reproduces every shelf.
    /// </summary>
    public sealed class Shop
    {
        /// <summary>Lowest tier a luxury shop offers ("all common items are removed").</summary>
        public const int LuxuryMinTier = 2;

        private static readonly ShopSlot[] SpecialCheckerSlots =
            { ShopSlot.SpecialChecker1, ShopSlot.SpecialChecker2, ShopSlot.SpecialChecker3 };

        private readonly RunState _run;
        private readonly ShopCatalog _catalog;
        private readonly IRandomProvider _random;
        private readonly ShopOffer[] _offers = new ShopOffer[Enum.GetValues(typeof(ShopSlot)).Length];

        // Reused by every draw so restocking does not allocate a new candidate list each time.
        private readonly List<ShopItem> _candidates = new List<ShopItem>();

        /// <summary>Is this a luxury visit (Phoenix Checker)?</summary>
        public bool IsLuxury { get; }

        /// <summary>Tier odds of the special checker shelves; null when every special checker is equally likely.</summary>
        public ShopTierOdds TierOdds { get; }

        /// <summary>Lowest tier the special checker shelves may offer this visit.</summary>
        private int MinTier => IsLuxury ? LuxuryMinTier : ShopItem.MinTier;

        public bool IsOpen { get; private set; } = true;

        /// <summary>Rerolls bought during this visit.</summary>
        public int RerollsUsed { get; private set; }

        public int RerollsLeft => Math.Max(0, _catalog.Prices.Rerolls.Count - RerollsUsed);

        /// <summary>What the next reroll costs; null when none is left.</summary>
        public int? NextRerollPrice => RerollsLeft > 0 ? _catalog.Prices.Rerolls[RerollsUsed] : (int?)null;

        /// <summary>Something changed: an item was bought, a checker sold, the shelves were rerolled or the shop closed.</summary>
        public event Action Changed;

        internal Shop(RunState run, ShopCatalog catalog, IRandomProvider random, bool isLuxury, ShopTierOdds tierOdds)
        {
            _run = run;
            _catalog = catalog;
            _random = random;
            IsLuxury = isLuxury;
            TierOdds = tierOdds;

            _offers[(int)ShopSlot.BasicChecker] = CreateOffer(catalog.BasicChecker);
            Restock();
        }

        /// <summary>What is on the shelf; null when the catalog had nothing left to put there.</summary>
        public ShopOffer GetOffer(ShopSlot slot) => _offers[(int)slot];

        #region Buying

        /// <summary>Could the shelf be bought right now? Has no side effects — the View colours prices with it.</summary>
        public PurchaseResult CheckPurchase(ShopSlot slot)
        {
            if (!IsOpen)
                return PurchaseResult.ShopClosed;

            ShopOffer offer = GetOffer(slot);
            if (offer == null)
                return PurchaseResult.EmptySlot;

            if (offer.IsSold)
                return PurchaseResult.AlreadySold;

            ShopItem item = offer.Item;
            if (IsOwned(item))
                return PurchaseResult.AlreadyOwned;

            if (_run.Deck.Count + item.CheckerCount > GameState.MaxCheckersPerPlayer)
                return PurchaseResult.DeckFull;

            if (item.Kind == ShopItemKind.SpecialChecker &&
                _run.SpecialCheckerCount + item.CheckerCount > _catalog.MaxSpecialCheckers)
                return PurchaseResult.TooManySpecialCheckers;

            if (_run.Gold < offer.Price)
                return PurchaseResult.NotEnoughGold;

            return PurchaseResult.Success;
        }

        /// <summary>Buys the shelf: the item goes to the run, the gold is spent and the shelf shows as sold.</summary>
        public PurchaseResult Buy(ShopSlot slot)
        {
            PurchaseResult result = CheckPurchase(slot);
            if (result != PurchaseResult.Success)
                return result;

            ShopOffer offer = GetOffer(slot);

            // Delivered first: if the item's factory failed, no gold would be lost.
            offer.Item.Deliver(_run, offer.Price);
            _run.SpendGold(offer.Price);

            // The plain checker shelf restocks itself.
            if (offer.Item.Kind != ShopItemKind.BasicChecker)
                offer.MarkSold();

            Changed?.Invoke();
            return PurchaseResult.Success;
        }

        #endregion

        #region Selling

        /// <summary>
        /// Fills <paramref name="result"/> with the checkers the player could sell: the deck's special checkers in
        /// deck order, then its extra plain checkers, the dearest first (the order they would be sold in).
        /// </summary>
        public void GetSellableCheckers(List<DeckEntry> result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            result.Clear();
            foreach (DeckEntry entry in _run.Deck)
            {
                if (entry.IsSpecial)
                    result.Add(entry);
            }

            // The plain checkers, dearest first; equally priced ones keep their deck order. Only the extra ones stay.
            int firstPlain = result.Count;
            foreach (DeckEntry entry in _run.Deck)
            {
                if (entry.IsSpecial)
                    continue;

                int at = result.Count;
                while (at > firstPlain && result[at - 1].PurchasePrice < entry.PurchasePrice)
                    at--;

                result.Insert(at, entry);
            }

            int keep = firstPlain + ExtraPlainCheckerCount;
            result.RemoveRange(keep, result.Count - keep);
        }

        /// <summary>
        /// Plain checkers the deck holds beyond the ones the run started with: those may be sold. Fewer plain checkers
        /// than that (some lost for good) leave none to spare.
        /// </summary>
        public int ExtraPlainCheckerCount
        {
            get
            {
                int plain = 0;
                foreach (DeckEntry entry in _run.Deck)
                {
                    if (!entry.IsSpecial)
                        plain++;
                }

                return Math.Max(0, plain - _run.Definition.StartingCheckers);
            }
        }

        /// <summary>What selling this checker pays: half of what was paid for it (<see cref="ShopPrices.GetSellPrice"/>).</summary>
        public int GetSellPrice(DeckEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return ShopPrices.GetSellPrice(entry.PurchasePrice);
        }

        /// <summary>Could this checker be sold right now? Has no side effects.</summary>
        public SaleResult CheckSale(DeckEntry entry)
        {
            if (!IsOpen)
                return SaleResult.ShopClosed;

            if (entry == null || !InDeck(entry))
                return SaleResult.NotInDeck;

            if (!entry.IsSpecial && ExtraPlainCheckerCount == 0)
                return SaleResult.NotForSale;

            return SaleResult.Success;
        }

        /// <summary>
        /// Sells a checker of the deck back: it leaves the deck at once — a special one frees its place among the
        /// special checkers — and its sell price is added to the gold. The shelves on show stay as they are; a later
        /// reroll may offer a sold special checker again. Any plain checker may be named while there is one to
        /// spare: they are alike, though what each one brings depends on what was paid for it.
        /// </summary>
        public SaleResult Sell(DeckEntry entry)
        {
            SaleResult result = CheckSale(entry);
            if (result != SaleResult.Success)
                return result;

            int price = GetSellPrice(entry);
            _run.RemoveFromDeck(entry);
            _run.EarnGold(price);

            Changed?.Invoke();
            return SaleResult.Success;
        }

        private bool InDeck(DeckEntry entry)
        {
            foreach (DeckEntry held in _run.Deck)
            {
                if (ReferenceEquals(held, entry))
                    return true;
            }

            return false;
        }

        #endregion

        #region Rerolling

        /// <summary>Pays for the next reroll and restocks every shelf except the plain checker.</summary>
        public RerollResult Reroll()
        {
            if (!IsOpen)
                return RerollResult.ShopClosed;

            int? price = NextRerollPrice;
            if (!price.HasValue)
                return RerollResult.NoRerollsLeft;

            if (_run.Gold < price.Value)
                return RerollResult.NotEnoughGold;

            _run.SpendGold(price.Value);
            RerollsUsed++;
            Restock();

            Changed?.Invoke();
            return RerollResult.Success;
        }

        #endregion

        /// <summary>Leaves the shop. Afterwards nothing can be bought and the run can start its next match.</summary>
        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            _run.HandleShopClosed(this);
            Changed?.Invoke();
        }

        #region Stocking

        private void Restock()
        {
            _candidates.Clear();
            foreach (ShopItem item in _catalog.SpecialCheckers)
            {
                if (item.Tier >= MinTier && !IsOwned(item))
                    _candidates.Add(item);
            }

            // Drawn without replacement, so the three shelves never show the same checker.
            foreach (ShopSlot slot in SpecialCheckerSlots)
                _offers[(int)slot] = CreateOffer(DrawSpecialChecker());

            _candidates.Clear();
            _candidates.AddRange(_catalog.Cards);
            _offers[(int)ShopSlot.Card] = CreateOffer(DrawCandidate(remove: false));

            _candidates.Clear();
            foreach (ShopItem charm in _catalog.Charms)
            {
                if (!IsOwned(charm))
                    _candidates.Add(charm);
            }

            _offers[(int)ShopSlot.Charm] = CreateOffer(DrawCandidate(remove: false));
        }

        /// <summary>
        /// Does the run already own this item, so that it can be neither offered nor bought? Charms do nothing
        /// twice, and a deck holds one of each special checker. Plain checkers and cards can always be had again.
        /// </summary>
        private bool IsOwned(ShopItem item)
        {
            switch (item.Kind)
            {
                case ShopItemKind.SpecialChecker: return _run.OwnsChecker(item.CheckerTypeId);
                case ShopItemKind.Charm: return _run.OwnsRelic(item.ItemId);
                default: return false;
            }
        }

        /// <summary>
        /// Takes one special checker out of the candidates for a shelf; null when nothing can go there. With tier
        /// odds the tier is rolled first and the checker drawn evenly within it; a rolled tier with nothing left
        /// gives way to the nearest allowed tier below it, then above it, so a lucky roll stays as lucky as it can.
        /// </summary>
        private ShopItem DrawSpecialChecker()
        {
            if (TierOdds == null)
                return DrawCandidate(remove: true);

            int rolled = TierOdds.RollTier(_random, MinTier);

            // The odds allow none of the tiers this visit may offer (a luxury visit in a stage that only weighs
            // tier 1): the luxury promise wins and every candidate is equally likely.
            if (rolled == 0)
                return DrawCandidate(remove: true);

            ShopItem item;
            for (int tier = rolled; tier >= MinTier; tier--)
            {
                if (TryDrawFromTier(tier, out item))
                    return item;
            }

            for (int tier = rolled + 1; tier <= ShopItem.MaxTier; tier++)
            {
                if (TryDrawFromTier(tier, out item))
                    return item;
            }

            return null;
        }

        /// <summary>Takes a candidate of this tier at random, if the odds allow the tier and one is left.</summary>
        private bool TryDrawFromTier(int tier, out ShopItem item)
        {
            item = null;
            if (TierOdds.GetWeight(tier) == 0)
                return false;

            int count = 0;
            foreach (ShopItem candidate in _candidates)
            {
                if (candidate.Tier == tier)
                    count++;
            }

            if (count == 0)
                return false;

            int pick = _random.Range(0, count);
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].Tier != tier || pick-- > 0)
                    continue;

                item = _candidates[i];
                _candidates.RemoveAt(i);
                return true;
            }

            return false;
        }

        private ShopItem DrawCandidate(bool remove)
        {
            if (_candidates.Count == 0)
                return null;

            int index = _random.Range(0, _candidates.Count);
            ShopItem item = _candidates[index];

            if (remove)
                _candidates.RemoveAt(index);

            return item;
        }

        private ShopOffer CreateOffer(ShopItem item)
        {
            return item != null ? new ShopOffer(item, PriceOf(item)) : null;
        }

        private int PriceOf(ShopItem item)
        {
            return Math.Max(0, _catalog.Prices.GetPrice(item) - _run.ShopDiscount);
        }

        #endregion
    }
}

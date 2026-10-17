using System;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Relics;

namespace Tavla.Core.Run
{
    /// <summary>
    /// Something the shop can sell. Immutable; built through the factory methods, which check the item once
    /// up front so a broken entry fails when the catalog is made, not when the player buys it.
    ///
    /// What buying it does depends on <see cref="Kind"/>: checkers join the deck,
    /// cards and charms join the run's relics. The price is not here — it belongs to the catalog's
    /// <see cref="ShopPrices"/>.
    /// </summary>
    public sealed class ShopItem
    {
        /// <summary>Lowest rarity tier of a special checker ("Sıradan").</summary>
        public const int MinTier = 1;

        /// <summary>Highest rarity tier of a special checker ("Özel").</summary>
        public const int MaxTier = 4;

        private readonly CheckerFactory _createChecker;
        private readonly Func<BaseRelic> _createRelic;

        public string ItemId { get; }
        public string DisplayName { get; }
        public ShopItemKind Kind { get; }

        /// <summary>Rarity tier (<see cref="MinTier"/>..<see cref="MaxTier"/>) of a special checker; 0 for anything else.</summary>
        public int Tier { get; }

        /// <summary>
        /// <see cref="BaseChecker.TypeId"/> of the checker a purchase adds to the deck; null for cards and charms.
        /// The View draws the item with that checker's art.
        /// </summary>
        public string CheckerTypeId { get; }

        /// <summary>Checkers one purchase adds to the deck: 1 for checkers, 0 for cards and charms.</summary>
        public int CheckerCount => _createChecker != null ? 1 : 0;

        private ShopItem(string itemId, string displayName, ShopItemKind kind, int tier, string checkerTypeId,
                         CheckerFactory createChecker, Func<BaseRelic> createRelic)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Kind = kind;
            Tier = tier;
            CheckerTypeId = checkerTypeId;
            _createChecker = createChecker;
            _createRelic = createRelic;
        }

        #region Factories

        /// <summary>A special checker of the given tier. Its id and name come from the checker's <see cref="IPurchasable"/>.</summary>
        /// <exception cref="ArgumentException">The checker does not describe itself as purchasable.</exception>
        public static ShopItem SpecialChecker(int tier, CheckerFactory create)
        {
            if (create == null)
                throw new ArgumentNullException(nameof(create));

            ThrowIfInvalidTier(tier);
            BaseChecker sample = create(0);
            IPurchasable purchasable = DescribeChecker(sample, nameof(create));

            return new ShopItem(purchasable.ItemId, purchasable.DisplayName, ShopItemKind.SpecialChecker, tier,
                                sample.TypeId, create, null);
        }

        /// <summary>A plain checker that adds one more to the deck.</summary>
        public static ShopItem BasicChecker()
        {
            return new ShopItem(DefaultChecker.Id, "Checker", ShopItemKind.BasicChecker, 0, DefaultChecker.Id,
                                ownerId => new DefaultChecker(ownerId), null);
        }

        /// <summary>A single-use card. A fresh one is made for every purchase.</summary>
        public static ShopItem Card(Func<ConsumableRelic> create)
        {
            BaseRelic sample = DescribeRelic(create, nameof(create));
            return new ShopItem(sample.ItemId, sample.DisplayName, ShopItemKind.Card, 0, null, null, create);
        }

        /// <summary>A permanent dice charm.</summary>
        public static ShopItem Charm(Func<PermanentRelic> create)
        {
            BaseRelic sample = DescribeRelic(create, nameof(create));
            return new ShopItem(sample.ItemId, sample.DisplayName, ShopItemKind.Charm, 0, null, null, create);
        }

        #endregion

        /// <summary>
        /// Hands the bought item to the run: checkers to the deck, which remembers the price paid so the checker
        /// can be sold back; cards and charms to the relics.
        /// </summary>
        internal void Deliver(RunState run, int price)
        {
            if (_createRelic != null)
            {
                run.AddRelic(_createRelic());
                return;
            }

            run.AddToDeck(_createChecker, price);
        }

        private static IPurchasable DescribeChecker(BaseChecker sample, string paramName)
        {
            if (sample == null)
                throw new ArgumentException("The checker factory returned null.", paramName);

            if (!(sample is IPurchasable purchasable))
                throw new ArgumentException("A special checker must implement IPurchasable to be sold.", paramName);

            return purchasable;
        }

        private static BaseRelic DescribeRelic(Func<BaseRelic> create, string paramName)
        {
            if (create == null)
                throw new ArgumentNullException(paramName);

            return create() ?? throw new ArgumentException("The relic factory returned null.", paramName);
        }

        private static void ThrowIfInvalidTier(int tier)
        {
            if (tier < MinTier || tier > MaxTier)
                throw new ArgumentOutOfRangeException(nameof(tier), tier, $"A tier is in the range {MinTier}..{MaxTier}.");
        }
    }
}

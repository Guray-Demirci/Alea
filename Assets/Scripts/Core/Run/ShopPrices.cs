using System;
using System.Collections.Generic;

namespace Tavla.Core.Run
{
    /// <summary>
    /// The shop's price list (GDD "Satın Alınabilir Eşyalar"). Immutable and validated on construction.
    /// These are base prices; <see cref="Shop"/> applies the Sacrificial Checker discount on top.
    /// </summary>
    public sealed class ShopPrices
    {
        private readonly int[] _tiers;
        private readonly int[] _rerolls;

        /// <summary>A plain checker ("Başlangıç Pulu").</summary>
        public int BasicChecker { get; }

        /// <summary>A single-use card.</summary>
        public int Card { get; }

        /// <summary>A permanent dice charm.</summary>
        public int Charm { get; }

        /// <summary>
        /// Cost of each reroll within one shop visit, in order. Its length is the number of rerolls a visit allows;
        /// every visit starts again from the first price.
        /// </summary>
        public IReadOnlyList<int> Rerolls => _rerolls;

        /// <param name="tierPrices">Special checker price per tier, from tier 1 to <see cref="ShopItem.MaxTier"/>.</param>
        /// <param name="rerollPrices">Reroll costs within one visit; empty for a shop without rerolls.</param>
        /// <exception cref="ArgumentNullException">A list is missing.</exception>
        /// <exception cref="ArgumentException">The wrong number of tier prices.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A negative price.</exception>
        public ShopPrices(int basicChecker, IReadOnlyList<int> tierPrices, int card, int charm, IReadOnlyList<int> rerollPrices)
        {
            if (tierPrices == null)
                throw new ArgumentNullException(nameof(tierPrices));

            if (rerollPrices == null)
                throw new ArgumentNullException(nameof(rerollPrices));

            if (tierPrices.Count != ShopItem.MaxTier)
                throw new ArgumentException($"One price per tier is needed ({ShopItem.MaxTier}).", nameof(tierPrices));

            ThrowIfNegative(basicChecker, nameof(basicChecker));
            ThrowIfNegative(card, nameof(card));
            ThrowIfNegative(charm, nameof(charm));

            _tiers = Copy(tierPrices, nameof(tierPrices));
            _rerolls = Copy(rerollPrices, nameof(rerollPrices));

            BasicChecker = basicChecker;
            Card = card;
            Charm = charm;
        }

        /// <summary>The price of a special checker of the given tier.</summary>
        public int GetTierPrice(int tier)
        {
            if (tier < ShopItem.MinTier || tier > ShopItem.MaxTier)
                throw new ArgumentOutOfRangeException(nameof(tier), tier,
                    $"A tier is in the range {ShopItem.MinTier}..{ShopItem.MaxTier}.");

            return _tiers[tier - ShopItem.MinTier];
        }

        /// <summary>The base price of an item, from its kind (and tier).</summary>
        public int GetPrice(ShopItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            switch (item.Kind)
            {
                case ShopItemKind.SpecialChecker: return GetTierPrice(item.Tier);
                case ShopItemKind.BasicChecker: return BasicChecker;
                case ShopItemKind.Card: return Card;
                case ShopItemKind.Charm: return Charm;
                default: throw new ArgumentOutOfRangeException(nameof(item), item.Kind, "Unknown item kind.");
            }
        }

        private static int[] Copy(IReadOnlyList<int> prices, string paramName)
        {
            int[] copy = new int[prices.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                ThrowIfNegative(prices[i], paramName);
                copy[i] = prices[i];
            }

            return copy;
        }

        private static void ThrowIfNegative(int price, string paramName)
        {
            if (price < 0)
                throw new ArgumentOutOfRangeException(paramName, price, "A price cannot be negative.");
        }
    }
}

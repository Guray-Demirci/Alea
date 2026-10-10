using System;
using System.Collections.Generic;
using Tavla.Core.Board;

namespace Tavla.Core.Run
{
    /// <summary>
    /// Everything a run's shop may offer, the price list and the deck rule the shop enforces.
    /// Immutable and validated on construction; every <see cref="Shop"/> visit draws from it.
    /// </summary>
    public sealed class ShopCatalog
    {
        /// <summary>GDD: "a player can hold at most 5 special checkers at the same time".</summary>
        public const int DefaultMaxSpecialCheckers = 5;

        private readonly ShopItem[] _specialCheckers;
        private readonly ShopItem[] _cards;
        private readonly ShopItem[] _charms;

        /// <summary>The special checkers the top slots draw from.</summary>
        public IReadOnlyList<ShopItem> SpecialCheckers => _specialCheckers;

        public IReadOnlyList<ShopItem> Cards => _cards;
        public IReadOnlyList<ShopItem> Charms => _charms;

        /// <summary>The plain checker of the basic slot.</summary>
        public ShopItem BasicChecker { get; } = ShopItem.BasicChecker();

        public ShopPrices Prices { get; }

        /// <summary>Most special checkers the player's deck may hold.</summary>
        public int MaxSpecialCheckers { get; }

        /// <exception cref="ArgumentNullException">A list or the prices are missing.</exception>
        /// <exception cref="ArgumentException">A null entry, an item of the wrong kind, or an item listed twice.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The special checker limit is outside 0..the deck size.</exception>
        public ShopCatalog(IReadOnlyList<ShopItem> specialCheckers, IReadOnlyList<ShopItem> cards,
                           IReadOnlyList<ShopItem> charms, ShopPrices prices,
                           int maxSpecialCheckers = DefaultMaxSpecialCheckers)
        {
            _specialCheckers = CopyOfKind(specialCheckers, ShopItemKind.SpecialChecker, nameof(specialCheckers));
            _cards = CopyOfKind(cards, ShopItemKind.Card, nameof(cards));
            _charms = CopyOfKind(charms, ShopItemKind.Charm, nameof(charms));
            Prices = prices ?? throw new ArgumentNullException(nameof(prices));

            if (maxSpecialCheckers < 0 || maxSpecialCheckers > GameState.MaxCheckersPerPlayer)
                throw new ArgumentOutOfRangeException(nameof(maxSpecialCheckers), maxSpecialCheckers,
                    $"The limit is in the range 0..{GameState.MaxCheckersPerPlayer}.");

            MaxSpecialCheckers = maxSpecialCheckers;
        }

        private static ShopItem[] CopyOfKind(IReadOnlyList<ShopItem> items, ShopItemKind kind, string paramName)
        {
            ShopItem[] copy = DefinitionList.Copy(items, paramName);
            HashSet<string> ids = new HashSet<string>();

            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].Kind != kind)
                    throw new ArgumentException($"Entry {i} ({copy[i].ItemId}) is a {copy[i].Kind}, not a {kind}.", paramName);

                if (!ids.Add(copy[i].ItemId))
                    throw new ArgumentException($"{copy[i].ItemId} is listed twice.", paramName);
            }

            return copy;
        }
    }
}

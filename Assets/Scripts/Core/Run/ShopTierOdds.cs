using System;
using System.Collections.Generic;
using Tavla.Core.Runtime;

namespace Tavla.Core.Run
{
    /// <summary>
    /// How likely each special checker tier is to fill a shop shelf. Every stage brings its own odds
    /// (<see cref="StageDefinition.ShopTierOdds"/>), so the shop gets better as the run goes on.
    /// Immutable and validated on construction.
    ///
    /// Weights are relative: {70, 25, 5, 0} makes a shelf tier 1 with a 70% chance, tier 2 25%, tier 3 5% and
    /// tier 4 never. A tier with no weight is never offered.
    /// </summary>
    public sealed class ShopTierOdds
    {
        private readonly int[] _weights;

        /// <param name="weights">One weight per tier, from tier 1 to <see cref="ShopItem.MaxTier"/>.</param>
        /// <exception cref="ArgumentNullException">No weights were given.</exception>
        /// <exception cref="ArgumentException">The wrong number of weights, or none above zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A negative weight.</exception>
        public ShopTierOdds(IReadOnlyList<int> weights)
        {
            if (weights == null)
                throw new ArgumentNullException(nameof(weights));

            if (weights.Count != ShopItem.MaxTier)
                throw new ArgumentException($"One weight per tier is needed ({ShopItem.MaxTier}).", nameof(weights));

            _weights = new int[weights.Count];
            int total = 0;

            for (int i = 0; i < _weights.Length; i++)
            {
                if (weights[i] < 0)
                    throw new ArgumentOutOfRangeException(nameof(weights), weights[i], "A weight cannot be negative.");

                _weights[i] = weights[i];
                total += weights[i];
            }

            if (total == 0)
                throw new ArgumentException("At least one tier needs a weight above zero.", nameof(weights));
        }

        /// <summary>The weight of a tier (<see cref="ShopItem.MinTier"/>..<see cref="ShopItem.MaxTier"/>).</summary>
        public int GetWeight(int tier)
        {
            if (tier < ShopItem.MinTier || tier > ShopItem.MaxTier)
                throw new ArgumentOutOfRangeException(nameof(tier), tier,
                    $"A tier is in the range {ShopItem.MinTier}..{ShopItem.MaxTier}.");

            return _weights[tier - ShopItem.MinTier];
        }

        /// <summary>
        /// Rolls a tier by the weights, leaving out the tiers below <paramref name="minTier"/> (a luxury visit skips
        /// tier 1): the weights left share the whole chance between them. Returns 0 when none of them has weight.
        /// </summary>
        public int RollTier(IRandomProvider random, int minTier = ShopItem.MinTier)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            int first = Math.Max(minTier, ShopItem.MinTier);
            int total = 0;
            for (int tier = first; tier <= ShopItem.MaxTier; tier++)
                total += GetWeight(tier);

            if (total == 0)
                return 0;

            int roll = random.Range(0, total);
            for (int tier = first; tier <= ShopItem.MaxTier; tier++)
            {
                roll -= GetWeight(tier);
                if (roll < 0)
                    return tier;
            }

            // Unreachable: the roll is below the total of the weights walked above.
            return ShopItem.MaxTier;
        }
    }
}

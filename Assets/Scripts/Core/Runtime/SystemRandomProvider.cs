using System;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Default randomness provider backed by <see cref="System.Random"/>.
    /// Given the same seed it produces the same sequence -> matches and tests are reproducible.
    /// </summary>
    public class SystemRandomProvider : IRandomProvider
    {
        private readonly Random _random;

        /// <summary>
        /// The seed this provider was created with. Log it so a reported bug can be replayed
        /// with exactly the same dice and chance outcomes.
        /// </summary>
        public int Seed { get; }

        public SystemRandomProvider() : this(Environment.TickCount) { }

        public SystemRandomProvider(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        public bool Chance(float probability) => _random.NextDouble() < probability;
    }
}

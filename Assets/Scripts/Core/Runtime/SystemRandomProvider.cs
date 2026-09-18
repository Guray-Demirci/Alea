using System;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Varsayilan rastgelelik saglayici. Tohum verilirse deterministik calisir -> testler tekrarlanabilir olur.
    /// </summary>
    public class SystemRandomProvider : IRandomProvider
    {
        private readonly Random _random;

        public SystemRandomProvider() : this(Environment.TickCount) { }

        public SystemRandomProvider(int seed)
        {
            _random = new Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        public bool Chance(float probability) => _random.NextDouble() < probability;
    }
}

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Abstraction of the randomness source. Most designed checkers rely on chances such as 50%/25%.
    /// UnityEngine.Random CANNOT be used (Core is pure C#). The abstraction also lets tests plug in a
    /// fixed-seed (deterministic) provider and reliably answer questions like
    /// "does the Bouncer really hit 50% of the time?".
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>Integer in the range [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>Returns true with the given probability in 0..1. E.g. Chance(0.5f) -> 50%.</summary>
        bool Chance(float probability);
    }
}

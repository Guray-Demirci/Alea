using Tavla.Core.Abilities;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Drunken Checker. "When you move it, it randomly travels one point less or one point more than the die
    /// (you rolled 4: it goes 3 or 5). It can wreck plans, but sometimes it hits an enemy you could not reach."
    /// It only decides which way it staggers; GameState checks the point it staggers to with the ordinary rules.
    /// </summary>
    public class DrunkenChecker : SpecialChecker, IDistanceDeviation
    {
        public const string Id = "checker_drunken";
        private const float ShortChance = 0.5f;

        public int RollDeviation(IRandomProvider random) => random.Chance(ShortChance) ? -1 : 1;

        public DrunkenChecker(int ownerId) : base(ownerId, Id, "Drunken Checker") { }
    }
}

using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Reckless Checker. "None of the penalties of the special tiles affect it."
    /// Swamp, Fog, Maelstrom, Toll Booth, Dungeon, Spike Pit, Powder Keg, Plunder Cove and Graveyard all leave it alone —
    /// without any of them knowing this class (see <see cref="BaseChecker.IsAffectedByTiles"/>).
    /// </summary>
    public class RecklessChecker : SpecialChecker, ITileImmunity
    {
        public const string Id = "checker_reckless";

        public RecklessChecker(int ownerId) : base(ownerId, Id, "Reckless Checker") { }
    }
}

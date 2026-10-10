using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Teleporter Checker. "When picked, it can be played to any point regardless of the dice, but you lose your
    /// second move."
    /// A point the dice reach is played normally; any other point ahead costs every die left (see IFreeMover).
    /// </summary>
    public class TeleporterChecker : SpecialChecker, IFreeMover
    {
        public const string Id = "checker_teleporter";

        public TeleporterChecker(int ownerId) : base(ownerId, Id, "Teleporter Checker") { }
    }
}

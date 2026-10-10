using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Gunpowder Checker. "If it is left open and the opponent captures it, it blows up the capturing checker too —
    /// both go to the bar. It scares the opponent away from your side of the board."
    /// A pure rule, so the whole ability is a marker; GameState sends the capturer to the bar once it has landed.
    /// </summary>
    public class GunpowderChecker : SpecialChecker, ICaptureRetaliation
    {
        public const string Id = "checker_gunpowder";

        public GunpowderChecker(int ownerId) : base(ownerId, Id, "Gunpowder Checker") { }
    }
}

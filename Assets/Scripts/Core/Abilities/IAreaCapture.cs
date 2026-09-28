using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Doomsday Checker: "If it lands exactly on a point the opponent has made,
    /// it captures ALL enemy checkers on that point at once."
    /// Classic backgammon forbids entering a made point; this ability inverts that rule.
    /// </summary>
    public interface IAreaCapture : ICheckerAbility
    {
        /// <summary>Can this checker capture every enemy checker on the target point?</summary>
        bool CanCaptureEntirePoint(Node target);
    }
}

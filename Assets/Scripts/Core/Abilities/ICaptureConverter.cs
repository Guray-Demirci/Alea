using Tavla.Core.Checkers;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Mutiny Checker: "When it captures an open enemy checker, the checker does not go to the bar: it is painted
    /// in your colour and becomes yours (as long as you do not exceed 15 checkers)."
    /// Only a checker that would have gone to the bar is converted — a Phoenix still flies home, a Sacrificial
    /// Checker is still destroyed. GameState checks the 15-checker and point-capacity limits; the ability only
    /// makes the new checker.
    /// </summary>
    public interface ICaptureConverter : ICheckerAbility
    {
        /// <summary>A NEW checker of the converter's owner that takes the victim's place.</summary>
        BaseChecker Convert(BaseChecker victim);
    }
}

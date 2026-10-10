using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Checkers that leave a <see cref="ScoreMark"/> on every point they land on — the Navigator Checker's footprints.
    ///
    /// GameState leaves the mark AS the checker lands, before a capture there is scored, so a capture made on
    /// landing already counts as made on the marked point. It is not an <see cref="IMoveReactor"/> for that reason:
    /// reactors run once the move — and its capture — is over.
    /// </summary>
    public interface IPointMarker : ICheckerAbility
    {
        /// <summary>The mark left on every point the checker lands on. Never null.</summary>
        ScoreMark LandingMark { get; }
    }
}

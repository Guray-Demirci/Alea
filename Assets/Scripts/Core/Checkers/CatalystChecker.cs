using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Catalyst Checker. "The points this checker provides are doubled."
    /// Only the score it earns ITSELF — its own captures and its own bear-off — is doubled; the score of
    /// the player's other checkers is untouched. Inside a Fog Tile the ability drops and it scores normally.
    /// </summary>
    public class CatalystChecker : SpecialChecker, IScoreModifier
    {
        public const string Id = "checker_catalyst";
        private const int ScoreFactor = 2;

        public int ModifyScore(int rawScore) => rawScore * ScoreFactor;

        public CatalystChecker(int ownerId) : base(ownerId, Id, "Catalyst Checker") { }
    }
}

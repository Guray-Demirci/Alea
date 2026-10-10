namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Checkers that change the score THEY earn — the points of their own captures and their own bear-off.
    /// Catalyst Checker: "the points this checker provides are doubled".
    /// It shapes only the RAW score of a single checker; the player's other scores are untouched.
    /// </summary>
    public interface IScoreModifier : ICheckerAbility
    {
        /// <summary>Must be a pure function: the bot's look-ahead calls it on cloned boards too.</summary>
        int ModifyScore(int rawScore);
    }
}

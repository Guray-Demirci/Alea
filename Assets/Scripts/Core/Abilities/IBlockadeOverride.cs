namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Checkers that break the blockade rule. Armored Checker: "Counts as a made point even when alone."
    /// The classic rule needs 2 checkers to make a point; this ability overrides that threshold per checker.
    /// </summary>
    public interface IBlockadeOverride : ICheckerAbility
    {
        bool FormsBlockadeAlone { get; }
    }
}

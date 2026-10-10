namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Reckless Checker: "None of the special tiles' penalties affect it."
    /// GameState routes every per-checker tile hook through <see cref="Checkers.BaseChecker.IsAffectedByTiles"/>,
    /// so no tile needs to know this checker: it simply never sees it land, leave, pass over or ask permission.
    /// </summary>
    public interface ITileImmunity : ICheckerAbility
    {
    }
}

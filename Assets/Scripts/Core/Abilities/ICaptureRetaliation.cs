namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Gunpowder Checker: "If it is captured, the checker that captured it is blown up too — both go to the bar."
    /// Only a capture made by LANDING on it has a capturer to blow up; a tile or a ranged ability (Clover,
    /// Grappling, Kraken) captures it from afar and is left untouched.
    /// </summary>
    public interface ICaptureRetaliation : ICheckerAbility
    {
    }
}

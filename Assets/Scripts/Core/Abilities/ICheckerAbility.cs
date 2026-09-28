namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Common marker interface for ALL checker abilities.
    ///
    /// Why it exists: the Fog Tile rule says "every special checker that enters this point
    /// loses ALL of its special abilities". Without a common base type such a blanket switch
    /// could not be written — every new checker would need its own branch in the Fog code.
    /// With this marker, <c>BaseChecker.GetAbility&lt;T&gt;()</c> can filter them all in one place.
    /// </summary>
    public interface ICheckerAbility
    {
    }
}

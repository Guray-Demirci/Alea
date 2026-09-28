using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Overrides the rule that a captured checker goes to the bar.
    /// Phoenix Checker -> HomeEntry (returns to its starting point), Sacrificial Checker -> Destroyed.
    /// </summary>
    public interface ICustomCaptureDestination : ICheckerAbility
    {
        CaptureDestination Destination { get; }
    }
}

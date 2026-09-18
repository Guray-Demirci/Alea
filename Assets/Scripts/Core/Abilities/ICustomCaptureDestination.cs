using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Kirilan pulun bara gitme kuralini ezer.
    /// Anka Pulu -> HomeEntry (baslangic hanesine doner), Fedakar Pul -> Destroyed (yok olur).
    /// </summary>
    public interface ICustomCaptureDestination : ICheckerAbility
    {
        CaptureDestination Destination { get; }
    }
}

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Where a captured checker goes. The two designed exceptions are covered by this enum:
    /// Phoenix Checker -> HomeEntry (its starting point instead of the bar), Sacrificial Checker -> Destroyed.
    /// </summary>
    public enum CaptureDestination
    {
        Bar = 0,        // Classic rule: goes to the bar
        HomeEntry = 1,  // Phoenix Checker: returns to its own starting point
        Destroyed = 2   // Sacrificial Checker / Powder Keg: removed from the game entirely
    }
}

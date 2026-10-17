namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Where a captured checker goes. The designed exceptions are covered by this enum:
    /// Phoenix Checker -> HomeEntry (its starting point instead of the bar), Sacrificial Checker -> Destroyed,
    /// a capture by the Mutiny Checker -> Converted, an opponent's checker with a Graveyard on the board -> Graveyard.
    /// </summary>
    public enum CaptureDestination
    {
        Bar = 0,        // Classic rule: goes to the bar
        HomeEntry = 1,  // Phoenix Checker: returns to its own starting point (the next free one if it is full)
        Destroyed = 2,  // Sacrificial Checker: removed from play for good — in a run, from the deck as well
        Converted = 3,  // Captured by the Mutiny Checker: leaves play, a checker of the capturer takes its place
        Graveyard = 4   // Graveyard Tile: the opponent's checker restarts on the Graveyard point instead of the bar
    }
}

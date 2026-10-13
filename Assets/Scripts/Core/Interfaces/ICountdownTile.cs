namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// A tile that counts down to its effect and then starts again (Powder Keg). The count belongs to the tile, not to
    /// a checker, so the View can show it on the point without knowing which tile it is.
    /// </summary>
    public interface ICountdownTile
    {
        /// <summary>Steps left before the effect, in what the tile counts (the Powder Keg: rounds); 1 = at the next.</summary>
        int Countdown { get; }
    }
}

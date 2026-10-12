namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// A tile that counts turns down to its effect and then starts again (Powder Keg). The count belongs to the tile,
    /// not to a checker, so the View can show it on the point without knowing which tile it is.
    /// </summary>
    public interface ICountdownTile
    {
        /// <summary>Turn ends left before the effect; 1 means it goes off when the current turn ends.</summary>
        int Countdown { get; }
    }
}

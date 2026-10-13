namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// A tile that opens and closes (Dungeon). Whether it is open belongs to the tile, so the View can show it on the
    /// point without knowing which tile it is.
    /// </summary>
    public interface IOpenCloseTile
    {
        /// <summary>Is the tile open right now — for the Dungeon, are its bars up?</summary>
        bool IsOpen { get; }
    }
}

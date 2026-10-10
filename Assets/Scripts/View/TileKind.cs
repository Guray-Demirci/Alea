namespace Tavla.View
{
    /// <summary>
    /// The tiles a <see cref="RunConfig"/> can place, as an Inspector dropdown.
    /// Assets store the NUMBER, so the values are explicit: never renumber an entry, only append new ones.
    /// </summary>
    public enum TileKind
    {
        Swamp = 0,
        Fog = 1,
        Maelstrom = 2,
        TollBooth = 3,
        Dungeon = 4,
        SpikePit = 5,
        PowderKeg = 6,
        PlunderCove = 7
    }
}

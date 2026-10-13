using Tavla.Core.Interfaces;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Plunder Cove Tile. "The pirates seize the bear-off and capture points earned on this point! Bearing a checker off
    /// from this tile or capturing an enemy checker on it earns the player no score at all."
    /// GameState applies it as it awards those two scores (<see cref="IScoreSeizingTile"/>), whoever earns them, so the
    /// tile itself has nothing to do. Flat bonuses, such as the Siege's, are not seized. A checker immune to tiles
    /// (Reckless) keeps what it earns here.
    /// </summary>
    public class PlunderCoveTile : BaseTileEffect, IScoreSeizingTile
    {
        public const string Id = "tile_plundercove";

        public override string EffectId => Id;
    }
}

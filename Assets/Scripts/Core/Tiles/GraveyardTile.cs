using Tavla.Core.Interfaces;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Graveyard Tile (Lanetli Mezarlık). "A captured checker normally goes to the bar and has to start over from the very
    /// beginning. When any of the opponent's checkers is captured, it restarts on this graveyard point instead of the bar.
    /// If the point holds as many of the opponent's checkers as it can, or the player's made point, it goes to the bar."
    /// Only the opponent's checkers rise here; the player's still go to the bar. The checker comes back as one entering
    /// from the bar would: it breaks a lone checker of the player's standing there. GameState applies all of it as it
    /// sends a checker to the bar (<see cref="IRestartPointTile"/>), so the tile itself has nothing to do.
    /// </summary>
    public class GraveyardTile : BaseTileEffect, IRestartPointTile
    {
        public const string Id = "tile_graveyard";

        public override string EffectId => Id;
    }
}

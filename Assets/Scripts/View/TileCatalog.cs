using System;
using Tavla.Core.Interfaces;
using Tavla.Core.Run;
using Tavla.Core.Tiles;

namespace Tavla.View
{
    /// <summary>
    /// The one place that maps the <see cref="TileKind"/> dropdown to tile classes. The run (<see cref="RunConfig"/>)
    /// and the sandbox both read it, so a new tile needs exactly one new case here.
    /// </summary>
    public static class TileCatalog
    {
        /// <summary>Makes a fresh tile of this kind on every call, as <see cref="TilePlacement"/> requires.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The kind has no tile class.</exception>
        public static Func<ITileEffect> GetFactory(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Swamp: return () => new SwampTile();
                case TileKind.Fog: return () => new FogTile();
                case TileKind.Maelstrom: return () => new MaelstromTile();
                case TileKind.TollBooth: return () => new TollBoothTile();
                case TileKind.Dungeon: return () => new DungeonTile();
                case TileKind.SpikePit: return () => new SpikePitTile();
                case TileKind.PowderKeg: return () => new PowderKegTile();
                case TileKind.PlunderCove: return () => new PlunderCoveTile();
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tile kind.");
            }
        }

        /// <summary>The <see cref="ITileEffect.EffectId"/> of this kind's tiles, which is how the board knows them.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The kind has no tile class.</exception>
        public static string GetEffectId(TileKind kind)
        {
            return GetFactory(kind)().EffectId;
        }

        /// <summary>A tile of this kind on the given point.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The kind has no tile class, or the point is off the board.</exception>
        public static TilePlacement CreatePlacement(TileKind kind, int point)
        {
            return new TilePlacement(point, GetFactory(kind));
        }
    }
}

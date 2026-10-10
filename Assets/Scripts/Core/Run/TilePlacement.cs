using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Run
{
    /// <summary>
    /// A tile the run puts on a point. It holds a FACTORY rather than a tile: tiles keep per-match state
    /// (Powder Keg fuses, Swamp locks, the Dungeon's open/closed cycle), so every match must get fresh
    /// instances. The factory must therefore return a new object on every call.
    /// </summary>
    public sealed class TilePlacement
    {
        private readonly Func<ITileEffect> _createTile;

        /// <summary>Board point index (0-23), the same numbering as <see cref="GameState.Nodes"/> and the logs.</summary>
        public int Point { get; }

        /// <exception cref="ArgumentOutOfRangeException">The point is not on the board.</exception>
        /// <exception cref="ArgumentNullException">No factory was given.</exception>
        public TilePlacement(int point, Func<ITileEffect> createTile)
        {
            if (!GameState.IsOnBoard(point))
                throw new ArgumentOutOfRangeException(nameof(point), point,
                    $"A tile must be placed on a point in the range 0..{GameState.NodeCount - 1}.");

            Point = point;
            _createTile = createTile ?? throw new ArgumentNullException(nameof(createTile));
        }

        /// <summary>Shorthand for tiles with a parameterless constructor: <c>TilePlacement.Of&lt;SwampTile&gt;(7)</c>.</summary>
        public static TilePlacement Of<T>(int point) where T : ITileEffect, new()
        {
            return new TilePlacement(point, () => new T());
        }

        /// <summary>The points the given tiles occupy, in the same order.</summary>
        internal static int[] GetPoints(IReadOnlyList<TilePlacement> tiles)
        {
            int[] points = new int[tiles.Count];
            for (int i = 0; i < points.Length; i++)
                points[i] = tiles[i].Point;

            return points;
        }

        /// <summary>A fresh tile for one match.</summary>
        /// <exception cref="InvalidOperationException">The factory returned null.</exception>
        public ITileEffect CreateTile()
        {
            ITileEffect tile = _createTile();
            if (tile == null)
                throw new InvalidOperationException($"The tile factory for point {Point} returned null.");

            return tile;
        }
    }
}

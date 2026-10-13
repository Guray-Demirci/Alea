using System;
using System.Collections.Generic;

namespace Tavla.Core.Run
{
    /// <summary>
    /// One stage of a run: a sequence of matches played one after another. A stage may bring new tiles; they are
    /// on the board for every match of the stage and STAY for the rest of the run, so the board only ever gets
    /// harder. A new tile on a point that already holds one from an earlier stage replaces it. Besides the tiles
    /// it names, a stage may have the run draw tiles at random (<see cref="RandomTileCount"/>). A stage also sets
    /// how likely each tier is in the shop, so the shop gets better as the board gets harder.
    /// Immutable and validated on construction.
    /// </summary>
    public sealed class StageDefinition
    {
        private readonly MatchDefinition[] _matches;
        private readonly TilePlacement[] _newTiles;

        /// <summary>The matches of the stage, in play order. Never empty.</summary>
        public IReadOnlyList<MatchDefinition> Matches => _matches;

        /// <summary>Tiles this stage adds to the board. The full board is <see cref="RunDefinition.GetTileLayout"/>.</summary>
        public IReadOnlyList<TilePlacement> NewTiles => _newTiles;

        /// <summary>
        /// Tiles the run draws from <see cref="RunDefinition.TilePool"/> when it enters this stage, each on a random
        /// point. They stay for the rest of the run like <see cref="NewTiles"/>; <see cref="RunState.Tiles"/> holds them.
        /// </summary>
        public int RandomTileCount { get; }

        /// <summary>
        /// Tier odds of the special checker shelves in the shops that lead into this stage's matches — the shop
        /// after the previous stage's boss included. Null: every special checker is equally likely, whatever its tier.
        /// </summary>
        public ShopTierOdds ShopTierOdds { get; }

        /// <param name="matches">At least one match.</param>
        /// <param name="newTiles">Tiles the stage adds; null or empty for none. At most one per point.</param>
        /// <param name="shopTierOdds">The stage's shop tier odds; null for none.</param>
        /// <param name="randomTileCount">Tiles the run draws at random when the stage begins; 0 for none.</param>
        /// <exception cref="ArgumentNullException">No match list was given.</exception>
        /// <exception cref="ArgumentException">No matches, a null entry, or two tiles on the same point.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A negative random tile count.</exception>
        public StageDefinition(IReadOnlyList<MatchDefinition> matches, IReadOnlyList<TilePlacement> newTiles = null,
                               ShopTierOdds shopTierOdds = null, int randomTileCount = 0)
        {
            if (randomTileCount < 0)
                throw new ArgumentOutOfRangeException(nameof(randomTileCount), randomTileCount,
                    "A stage cannot draw a negative number of tiles.");

            ShopTierOdds = shopTierOdds;
            RandomTileCount = randomTileCount;

            _matches = DefinitionList.Copy(matches, nameof(matches));
            if (_matches.Length == 0)
                throw new ArgumentException("A stage needs at least one match.", nameof(matches));

            _newTiles = newTiles != null ? DefinitionList.Copy(newTiles, nameof(newTiles)) : Array.Empty<TilePlacement>();

            for (int i = 0; i < _newTiles.Length; i++)
            {
                for (int j = i + 1; j < _newTiles.Length; j++)
                {
                    if (_newTiles[i].Point == _newTiles[j].Point)
                        throw new ArgumentException(
                            $"Tiles {i} and {j} are both on point {_newTiles[i].Point}.", nameof(newTiles));
                }
            }
        }
    }
}

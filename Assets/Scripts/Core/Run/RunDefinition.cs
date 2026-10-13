using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Run
{
    /// <summary>
    /// A whole run as designed: stages of matches, the tiles each stage adds, the player's starting deck and the
    /// run's economy. Immutable and fully validated on construction, so a run never breaks halfway through
    /// because of a mistake in its data. <see cref="RunState"/> plays it.
    ///
    /// The first game mode is 3 stages of 3 matches, but nothing here assumes those numbers.
    /// </summary>
    public sealed class RunDefinition
    {
        private readonly StageDefinition[] _stages;

        // The board of each stage, built once: earlier stages' tiles with the stage's own tiles on top.
        private readonly TilePlacement[][] _tileLayouts;

        private readonly Func<ITileEffect>[] _tilePool;
        private readonly Func<ITileEffect>[] _drawableTiles;
        private readonly int[] _drawablePoints;

        /// <summary>The stages in play order. Never empty.</summary>
        public IReadOnlyList<StageDefinition> Stages => _stages;

        /// <summary>
        /// The tiles a stage's <see cref="StageDefinition.RandomTileCount"/> is drawn from, each listed once. A tile
        /// comes at most once a run: one a stage places itself is never drawn, nor is one drawn before.
        /// </summary>
        public IReadOnlyList<Func<ITileEffect>> TilePool => _tilePool;

        /// <summary>The pool without the tiles some stage places itself: what the random draws may still bring.</summary>
        internal IReadOnlyList<Func<ITileEffect>> DrawableTiles => _drawableTiles;

        /// <summary>
        /// The points no stage ever places a tile on, in order: a drawn tile goes on one of them, so a stage's own
        /// tile never covers it and it never covers one.
        /// </summary>
        internal IReadOnlyList<int> DrawablePoints => _drawablePoints;

        /// <summary>Default checkers in the player's deck when the run begins. The shop adds to them.</summary>
        public int StartingCheckers { get; }

        /// <summary>
        /// Turns left to the player once the opponent runs out of checkers; 0 ends the match instead.
        /// The same for every match (see <see cref="MatchRules.FinalStretchTurns"/>).
        /// </summary>
        public int FinalStretchTurns { get; }

        public RunEconomy Economy { get; }

        /// <summary>What the shop between matches sells and for how much; null for a run without a shop.</summary>
        public ShopCatalog Shop { get; }

        /// <summary>Matches in the whole run, over all stages.</summary>
        public int MatchCount { get; }

        /// <param name="tilePool">The tiles random draws come from (<see cref="TilePool"/>); null or empty for none.</param>
        /// <exception cref="ArgumentNullException">No stage list or no economy was given.</exception>
        /// <exception cref="ArgumentException">
        /// No stages, a null entry, a stage whose tiles — its random ones counted at the worst points — leave too
        /// little room to deal the starting checkers, a pool tile that is null or listed twice, or more random
        /// draws than the pool has tiles no stage places itself.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">A starting deck or final stretch outside its range.</exception>
        public RunDefinition(IReadOnlyList<StageDefinition> stages, int startingCheckers, int finalStretchTurns,
                             RunEconomy economy, ShopCatalog shop = null, IReadOnlyList<Func<ITileEffect>> tilePool = null)
        {
            Shop = shop;

            _stages = DefinitionList.Copy(stages, nameof(stages));
            if (_stages.Length == 0)
                throw new ArgumentException("A run needs at least one stage.", nameof(stages));

            _tilePool = tilePool != null ? DefinitionList.Copy(tilePool, nameof(tilePool)) : Array.Empty<Func<ITileEffect>>();
            string[] poolIds = GetPoolIds(_tilePool);

            if (startingCheckers < GameState.MinCheckersPerPlayer || startingCheckers > GameState.MaxCheckersPerPlayer)
                throw new ArgumentOutOfRangeException(nameof(startingCheckers), startingCheckers,
                    $"The player starts with {GameState.MinCheckersPerPlayer}..{GameState.MaxCheckersPerPlayer} checkers.");

            if (finalStretchTurns < 0)
                throw new ArgumentOutOfRangeException(nameof(finalStretchTurns), finalStretchTurns,
                    "The final stretch cannot be negative; use 0 to end the match instead.");

            StartingCheckers = startingCheckers;
            FinalStretchTurns = finalStretchTurns;
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));

            for (int s = 0; s < _stages.Length; s++)
                MatchCount += _stages[s].Matches.Count;

            _tileLayouts = BuildTileLayouts(_stages);

            // Starting checkers never go on a tile, so a stage covered in tiles could not be dealt. Where the random
            // tiles land is not known yet, so each is counted as if it took a free point from both sides.
            int randomTiles = 0;
            for (int s = 0; s < _tileLayouts.Length; s++)
            {
                randomTiles += _stages[s].RandomTileCount;
                if (!StartingLayout.HasRoomAround(TilePlacement.GetPoints(_tileLayouts[s]), randomTiles))
                    throw new ArgumentException(
                        $"Stage {s} has too many tiles: the starting checkers need more free points.", nameof(stages));
            }

            _drawableTiles = FindDrawableTiles(_tilePool, poolIds, _stages);
            if (randomTiles > _drawableTiles.Length)
                throw new ArgumentException(
                    $"The stages draw {randomTiles} random tiles, but the tile pool has only {_drawableTiles.Length} " +
                    "that no stage places itself, and a tile comes at most once a run.", nameof(tilePool));

            // Tiles are only ever replaced, never removed, so the last stage holds every point a stage puts a tile
            // on. The room check above leaves enough of the others for every random tile.
            _drawablePoints = FindFreePoints(_tileLayouts[_tileLayouts.Length - 1]);
        }

        /// <summary>
        /// Every tile a stage places itself on the board during the given stage — its own tiles on top of the ones
        /// earlier stages left behind — ordered by point. The run's random tiles are not here: they are drawn as
        /// the run is played (<see cref="RunState.Tiles"/>).
        /// </summary>
        public IReadOnlyList<TilePlacement> GetTileLayout(int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= _stages.Length)
                throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex,
                    $"Stage index must be in the range 0..{_stages.Length - 1}.");

            return _tileLayouts[stageIndex];
        }

        private static TilePlacement[][] BuildTileLayouts(StageDefinition[] stages)
        {
            TilePlacement[][] layouts = new TilePlacement[stages.Length][];

            // One slot per point: a later stage's tile simply overwrites the earlier one on the same point.
            TilePlacement[] byPoint = new TilePlacement[GameState.NodeCount];
            List<TilePlacement> layout = new List<TilePlacement>(GameState.NodeCount);

            for (int s = 0; s < stages.Length; s++)
            {
                IReadOnlyList<TilePlacement> newTiles = stages[s].NewTiles;
                for (int t = 0; t < newTiles.Count; t++)
                    byPoint[newTiles[t].Point] = newTiles[t];

                layout.Clear();
                for (int p = 0; p < byPoint.Length; p++)
                {
                    if (byPoint[p] != null)
                        layout.Add(byPoint[p]);
                }

                layouts[s] = layout.ToArray();
            }

            return layouts;
        }

        /// <summary>The <see cref="ITileEffect.EffectId"/> of every pool tile, in order. Each tile may be listed once.</summary>
        private static string[] GetPoolIds(Func<ITileEffect>[] pool)
        {
            string[] ids = new string[pool.Length];
            for (int i = 0; i < pool.Length; i++)
            {
                ITileEffect tile = pool[i]();
                if (tile == null)
                    throw new ArgumentException($"Tile pool entry {i} returned null.", "tilePool");

                ids[i] = tile.EffectId;
                if (Array.IndexOf(ids, ids[i], 0, i) >= 0)
                    throw new ArgumentException($"The tile pool lists {ids[i]} twice.", "tilePool");
            }

            return ids;
        }

        private static Func<ITileEffect>[] FindDrawableTiles(Func<ITileEffect>[] pool, string[] poolIds,
                                                             StageDefinition[] stages)
        {
            if (pool.Length == 0)
                return pool;

            HashSet<string> placed = new HashSet<string>();
            for (int s = 0; s < stages.Length; s++)
            {
                IReadOnlyList<TilePlacement> tiles = stages[s].NewTiles;
                for (int t = 0; t < tiles.Count; t++)
                    placed.Add(tiles[t].CreateTile().EffectId);
            }

            List<Func<ITileEffect>> drawable = new List<Func<ITileEffect>>(pool.Length);
            for (int i = 0; i < pool.Length; i++)
            {
                if (!placed.Contains(poolIds[i]))
                    drawable.Add(pool[i]);
            }

            return drawable.ToArray();
        }

        private static int[] FindFreePoints(TilePlacement[] layout)
        {
            bool[] taken = new bool[GameState.NodeCount];
            for (int t = 0; t < layout.Length; t++)
                taken[layout[t].Point] = true;

            List<int> free = new List<int>(GameState.NodeCount);
            for (int p = 0; p < taken.Length; p++)
            {
                if (!taken[p])
                    free.Add(p);
            }

            return free.ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using Tavla.Core.Board;

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

        /// <summary>The stages in play order. Never empty.</summary>
        public IReadOnlyList<StageDefinition> Stages => _stages;

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

        /// <exception cref="ArgumentNullException">No stage list or no economy was given.</exception>
        /// <exception cref="ArgumentException">
        /// No stages, a null entry, or a stage whose tiles leave too little room to deal the starting checkers.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">A starting deck or final stretch outside its range.</exception>
        public RunDefinition(IReadOnlyList<StageDefinition> stages, int startingCheckers, int finalStretchTurns,
                             RunEconomy economy, ShopCatalog shop = null)
        {
            Shop = shop;

            _stages = DefinitionList.Copy(stages, nameof(stages));
            if (_stages.Length == 0)
                throw new ArgumentException("A run needs at least one stage.", nameof(stages));

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

            // Starting checkers never go on a tile, so a stage covered in tiles could not be dealt.
            for (int s = 0; s < _tileLayouts.Length; s++)
            {
                if (!StartingLayout.HasRoomAround(TilePlacement.GetPoints(_tileLayouts[s])))
                    throw new ArgumentException(
                        $"Stage {s} has too many tiles: the starting checkers need more free points.", nameof(stages));
            }
        }

        /// <summary>
        /// Every tile on the board during the given stage — the stage's own tiles on top of the ones earlier
        /// stages left behind — ordered by point.
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
    }
}

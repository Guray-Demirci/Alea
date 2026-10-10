using System;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Board;
using Tavla.View;

namespace Tavla.Sandbox
{
    /// <summary>
    /// What the sandbox deals: the player's special checkers, both sides' checker counts, the tiles and the rules.
    /// Only DATA lives here; <see cref="SandboxMatchFactory"/> turns it into a match. <see cref="GodModePanel"/>
    /// edits it while playing and remembers the last one it dealt between Play sessions.
    /// </summary>
    [Serializable]
    public sealed class SandboxSetup
    {
        [Serializable]
        public sealed class TileEntry
        {
            public TileKind Tile;

            [Tooltip("Board point index 0-23, as printed in the logs.")]
            [Range(0, GameState.NodeCount - 1)] public int Point;
        }

        [Tooltip("The player's special checkers, one of each, as in a deck. The player's other checkers are plain.")]
        public List<CheckerKind> PlayerSpecials = new List<CheckerKind>();

        [Tooltip("All of the player's checkers, the special ones included.")]
        [Range(GameState.MinCheckersPerPlayer, GameState.MaxCheckersPerPlayer)]
        public int PlayerCheckers = GameState.MaxCheckersPerPlayer;

        [Tooltip("The opponent's checkers, all plain — as in a run.")]
        [Range(GameState.MinCheckersPerPlayer, GameState.MaxCheckersPerPlayer)]
        public int OpponentCheckers = GameState.MaxCheckersPerPlayer;

        [Tooltip("Tiles on the board. A point listed twice keeps the later tile.")]
        public List<TileEntry> Tiles = new List<TileEntry>();

        [Tooltip("Score the player must reach. 0 = classic rules: the higher score wins when the turns run out.")]
        [Min(0)] public int TargetScore;

        [Tooltip("Classic: turns of both players. With a target: only the player's turns.")]
        [Min(1)] public int TurnLimit = 99;

        [Tooltip("With a target: turns left to the player once the opponent runs out of checkers. 0 = the match ends.")]
        [Min(0)] public int FinalStretchTurns = 3;

        [Tooltip("Gold the player starts the match with (Bandit, Toll Booth and Golden work with gold).")]
        [Min(0)] public int StartingGold;
    }
}

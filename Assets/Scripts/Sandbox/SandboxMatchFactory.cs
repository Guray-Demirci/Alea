using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Run;
using Tavla.Core.Runtime;
using Tavla.View;

namespace Tavla.Sandbox
{
    /// <summary>
    /// Deals a sandbox match from a <see cref="SandboxSetup"/>, step for step the way a run deals one
    /// (<see cref="RunState.StartMatch"/>): the rules, a <see cref="StartingLayout"/> that keeps the tiles free,
    /// both sides' checkers, fresh tiles and the player's gold — only the player's checkers are picked by hand
    /// instead of bought. It uses nothing but Core's public setup API, so the match plays by the game's own rules.
    /// </summary>
    public static class SandboxMatchFactory
    {
        /// <summary>The human's seat. The sandbox scene keeps BoardManager's bot on the other one.</summary>
        public const int PlayerId = 0;

        /// <summary>
        /// A new match, ready for <see cref="BoardManager.PlayMatch"/>. The seed reproduces it exactly — the layout
        /// and every roll and chance after it — as long as the setup is the same.
        /// </summary>
        /// <exception cref="ArgumentNullException">No setup.</exception>
        /// <exception cref="ArgumentException">
        /// The setup cannot be dealt: more special checkers than checkers, a count or rule out of range, or tiles
        /// that leave no room to deal the checkers around them.
        /// </exception>
        public static GameState Create(SandboxSetup setup, int seed, out MatchContext context)
        {
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));

            if (setup.PlayerSpecials.Count > setup.PlayerCheckers)
                throw new ArgumentException(
                    $"{setup.PlayerSpecials.Count} special checkers do not fit in {setup.PlayerCheckers} checkers.");

            TilePlacement[] tiles = CreateTiles(setup.Tiles);
            int[] tilePoints = new int[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
                tilePoints[i] = tiles[i].Point;

            if (!StartingLayout.HasRoomAround(tilePoints))
                throw new ArgumentException("Too many tiles: no room is left to deal the checkers around them.");

            MatchRules rules = setup.TargetScore > 0
                ? MatchRules.ForTarget(setup.TargetScore, setup.TurnLimit, setup.FinalStretchTurns, PlayerId)
                : MatchRules.Classic(setup.TurnLimit);

            GameState board = new GameState(rules);
            SystemRandomProvider random = new SystemRandomProvider(seed);
            context = new MatchContext(board, random);

            int opponentId = GameState.GetOpponent(PlayerId);
            int[] counts = new int[GameState.PlayerCount];
            counts[PlayerId] = setup.PlayerCheckers;
            counts[opponentId] = setup.OpponentCheckers;

            StartingLayout layout = StartingLayout.Deal(counts[0], counts[1], random, tilePoints);
            board.SetupPlayer(PlayerId, CreatePlayerCheckers(setup.PlayerSpecials, layout), layout);
            board.SetupPlayer(opponentId, CreateDefaultCheckers(opponentId, counts[opponentId]), layout);

            for (int i = 0; i < tiles.Length; i++)
                board.PlaceTile(tiles[i].Point, tiles[i].CreateTile());

            if (setup.StartingGold > 0)
                context.AddGold(PlayerId, setup.StartingGold);

            return board;
        }

        private static TilePlacement[] CreateTiles(IReadOnlyList<SandboxSetup.TileEntry> entries)
        {
            TilePlacement[] tiles = new TilePlacement[entries.Count];
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = TileCatalog.CreatePlacement(entries[i].Tile, entries[i].Point);

            return tiles;
        }

        /// <summary>
        /// The player's checkers in layout slot order. The special ones go on top of the stacks, one stack after
        /// another, so each starts in sight and none hides under another; plain checkers fill the rest.
        /// </summary>
        private static BaseChecker[] CreatePlayerCheckers(IReadOnlyList<CheckerKind> specials, StartingLayout layout)
        {
            BaseChecker[] checkers = new BaseChecker[layout.GetCheckerCount(PlayerId)];
            List<int> slots = GetSlotsTopFirst(layout, PlayerId);

            for (int i = 0; i < specials.Count; i++)
                checkers[slots[i]] = CheckerCatalog.GetFactory(specials[i])(PlayerId);

            for (int slot = 0; slot < checkers.Length; slot++)
            {
                if (checkers[slot] == null)
                    checkers[slot] = new DefaultChecker(PlayerId);
            }

            return checkers;
        }

        /// <summary>
        /// Every slot of the player, by depth from the top of its stack: the top slot of every stack first, then the
        /// one under each, and so on. Slots in a row on the same point form a stack, pushed bottom first.
        /// </summary>
        private static List<int> GetSlotsTopFirst(StartingLayout layout, int playerId)
        {
            int count = layout.GetCheckerCount(playerId);

            List<List<int>> stacks = new List<List<int>>();
            for (int slot = 0; slot < count; slot++)
            {
                if (slot == 0 || layout.GetPoint(playerId, slot) != layout.GetPoint(playerId, slot - 1))
                    stacks.Add(new List<int>());

                stacks[stacks.Count - 1].Add(slot);
            }

            List<int> ordered = new List<int>(count);
            for (int depth = 0; ordered.Count < count; depth++)
            {
                for (int s = 0; s < stacks.Count; s++)
                {
                    List<int> stack = stacks[s];
                    if (depth < stack.Count)
                        ordered.Add(stack[stack.Count - 1 - depth]);
                }
            }

            return ordered;
        }

        private static BaseChecker[] CreateDefaultCheckers(int ownerId, int count)
        {
            BaseChecker[] checkers = new BaseChecker[count];
            for (int i = 0; i < count; i++)
                checkers[i] = new DefaultChecker(ownerId);

            return checkers;
        }
    }
}

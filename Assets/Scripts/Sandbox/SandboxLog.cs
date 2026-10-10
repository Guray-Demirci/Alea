using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Sandbox
{
    /// <summary>
    /// What happened in the sandbox match, one short line per event Core reports: rolls, moves, every ability and
    /// tile that fired, captures, score and gold. Testing a checker comes down to "did its ability fire, and what
    /// did it do?" — this answers it on the panel, without the console. Keeps the last <see cref="Capacity"/> lines.
    /// </summary>
    public sealed class SandboxLog
    {
        public const int Capacity = 100;

        private readonly List<string> _lines = new List<string>(Capacity);

        private GameState _board;
        private MatchContext _context;

        public IReadOnlyList<string> Lines => _lines;

        /// <summary>Goes up with every line, so the panel knows when to scroll to the newest.</summary>
        public int Version { get; private set; }

        /// <summary>The name a player reads: the checker's shop name, or "Checker" for a plain one.</summary>
        public static string NameOf(BaseChecker checker)
        {
            if (checker == null)
                return "?";

            IPurchasable item = checker as IPurchasable;
            return item != null ? item.DisplayName : "Checker";
        }

        /// <summary>Follows this match from now on; the previous one is dropped. Attach before the first roll.</summary>
        public void Attach(GameState board, MatchContext context)
        {
            Detach();

            _board = board;
            _context = context;

            _board.TurnChanged += HandleTurnChanged;
            _board.MoveExecuted += HandleMoveExecuted;
            _board.MoveRejected += HandleMoveRejected;
            _board.AbilityTriggered += HandleAbilityTriggered;
            _board.TileTriggered += HandleTileTriggered;
            _board.CheckerCaptured += HandleCheckerCaptured;
            _board.CheckerLostForGood += HandleCheckerLostForGood;
            _board.CheckerAdded += HandleCheckerAdded;
            _board.CheckerTeleported += HandleCheckerTeleported;
            _board.CheckerReleased += HandleCheckerReleased;
            _board.CheckerSacrificed += HandleCheckerSacrificed;
            _board.FinalStretchStarted += HandleFinalStretchStarted;
            _board.MatchEnded += HandleMatchEnded;

            _context.DiceRolled += HandleDiceRolled;
            _context.ScoreChanged += HandleScoreChanged;
            _context.GoldChanged += HandleGoldChanged;
            _context.TurnSkipped += HandleTurnSkipped;
        }

        /// <summary>Stops following the match. Safe to call more than once.</summary>
        public void Detach()
        {
            if (_board != null)
            {
                _board.TurnChanged -= HandleTurnChanged;
                _board.MoveExecuted -= HandleMoveExecuted;
                _board.MoveRejected -= HandleMoveRejected;
                _board.AbilityTriggered -= HandleAbilityTriggered;
                _board.TileTriggered -= HandleTileTriggered;
                _board.CheckerCaptured -= HandleCheckerCaptured;
                _board.CheckerLostForGood -= HandleCheckerLostForGood;
                _board.CheckerAdded -= HandleCheckerAdded;
                _board.CheckerTeleported -= HandleCheckerTeleported;
                _board.CheckerReleased -= HandleCheckerReleased;
                _board.CheckerSacrificed -= HandleCheckerSacrificed;
                _board.FinalStretchStarted -= HandleFinalStretchStarted;
                _board.MatchEnded -= HandleMatchEnded;
                _board = null;
            }

            if (_context != null)
            {
                _context.DiceRolled -= HandleDiceRolled;
                _context.ScoreChanged -= HandleScoreChanged;
                _context.GoldChanged -= HandleGoldChanged;
                _context.TurnSkipped -= HandleTurnSkipped;
                _context = null;
            }
        }

        public void Clear()
        {
            _lines.Clear();
            Version++;
        }

        public void Add(string line)
        {
            if (_lines.Count == Capacity)
                _lines.RemoveAt(0);

            _lines.Add(line);
            Version++;
        }

        private static string Side(int playerId) => $"P{playerId}";

        private static string Where(int index)
        {
            if (index == GameState.BarIndex)
                return "bar";

            return index == GameState.BearOffIndex ? "off" : index.ToString();
        }

        #region Handlers

        private void HandleTurnChanged(int playerId) => Add($"--- {Side(playerId)}'s turn ---");

        private void HandleDiceRolled(DiceRoll roll)
        {
            string dice = roll.Second == 0
                ? $"{roll.First} (one die)"
                : $"{roll.First}-{roll.Second}{(roll.IsDouble ? " double" : string.Empty)}";

            Add($"{Side(_board.CurrentPlayerId)} rolled {dice}");
        }

        private void HandleMoveExecuted(MoveResult result)
        {
            string line = $"{Side(result.PlayerId)} {NameOf(result.MovedChecker)} " +
                          $"{Where(result.FromIndex)} -> {(result.WasBearOff ? "off" : Where(result.ToIndex))}";

            if (result.HasCapture)
                line += $", hit {result.CapturedCheckers.Count}";

            if (result.WasInfiltration)
                line += ", infiltrated";

            if (result.ScoreGained != 0)
                line += $", +{result.ScoreGained}";

            Add(line);
        }

        private void HandleMoveRejected(MoveResult result) => Add($"rejected: {result.Error}");

        private void HandleAbilityTriggered(string abilityId, BaseChecker checker, int nodeIndex) =>
            Add($"* {abilityId}: {NameOf(checker)} @{Where(nodeIndex)}");

        private void HandleTileTriggered(ITileEffect tile, int nodeIndex, BaseChecker checker) =>
            Add($"# {tile.EffectId} @{Where(nodeIndex)}{(checker != null ? $": {NameOf(checker)}" : string.Empty)}");

        private void HandleCheckerCaptured(BaseChecker checker, int nodeIndex, CaptureDestination destination) =>
            Add($"{NameOf(checker)} ({Side(checker.OwnerId)}) hit @{Where(nodeIndex)} -> {destination}");

        private void HandleCheckerLostForGood(BaseChecker checker, int nodeIndex) =>
            Add($"{NameOf(checker)} ({Side(checker.OwnerId)}) lost for good @{Where(nodeIndex)}");

        private void HandleCheckerAdded(BaseChecker checker, int nodeIndex) =>
            Add($"{NameOf(checker)} joined {Side(checker.OwnerId)} @{Where(nodeIndex)}");

        private void HandleCheckerTeleported(BaseChecker checker, int fromIndex, int toIndex) =>
            Add($"{NameOf(checker)} pulled {Where(fromIndex)} -> {Where(toIndex)}");

        private void HandleCheckerReleased(BaseChecker checker, int nodeIndex) =>
            Add($"{NameOf(checker)} freed @{Where(nodeIndex)} (a die spent)");

        private void HandleCheckerSacrificed(BaseChecker checker, int nodeIndex) =>
            Add($"{NameOf(checker)} sacrificed @{Where(nodeIndex)}");

        private void HandleFinalStretchStarted(int remaining) => Add($"final stretch: {remaining} turns");

        private void HandleMatchEnded(int winnerId) =>
            Add(winnerId == GameState.NoWinner ? "MATCH OVER: draw" : $"MATCH OVER: {Side(winnerId)} wins");

        private void HandleScoreChanged(int playerId, int score) => Add($"{Side(playerId)} score {score}");

        private void HandleGoldChanged(int playerId, int gold) => Add($"{Side(playerId)} gold {gold}");

        private void HandleTurnSkipped(int playerId) => Add($"{Side(playerId)} will skip a turn");

        #endregion
    }
}

using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// Writes what Core reports during a match to the console: moves, captures, abilities, tiles, turns, dice, score
    /// and gold. The lines go through <see cref="GameLog"/>, so they cost nothing in release builds.
    ///
    /// Kept apart from <see cref="BoardManager"/>, which only listens to the events that change what is on screen.
    /// Attach it before the View's own listeners so a turn's line comes before the dice its listener rolls.
    /// </summary>
    internal sealed class BoardEventLog
    {
        private GameState _board;
        private MatchContext _context;

        /// <summary>Follows this match from now on; the previous one is dropped.</summary>
        public void Attach(GameState board, MatchContext context)
        {
            Detach();

            _board = board;
            _context = context;

            _board.MoveExecuted += HandleMoveExecuted;
            _board.MoveRejected += HandleMoveRejected;
            _board.CheckerCaptured += HandleCheckerCaptured;
            _board.CheckerDestroyed += HandleCheckerDestroyed;
            _board.CheckerAdded += HandleCheckerAdded;
            _board.CheckerTeleported += HandleCheckerTeleported;
            _board.CheckerReleased += HandleCheckerReleased;
            _board.CheckerSacrificed += HandleCheckerSacrificed;
            _board.TileTriggered += HandleTileTriggered;
            _board.AbilityTriggered += HandleAbilityTriggered;
            _board.TurnChanged += HandleTurnChanged;
            _board.TurnCountChanged += HandleTurnCountChanged;
            _board.CheckerBorneOff += HandleCheckerBorneOff;
            _board.FinalStretchStarted += HandleFinalStretchStarted;
            _board.MatchEnded += HandleMatchEnded;

            _context.DiceRolled += HandleDiceRolled;
            _context.ScoreChanged += HandleScoreChanged;
            _context.GoldChanged += HandleGoldChanged;
        }

        /// <summary>Stops following the match. Safe to call more than once.</summary>
        public void Detach()
        {
            if (_board != null)
            {
                _board.MoveExecuted -= HandleMoveExecuted;
                _board.MoveRejected -= HandleMoveRejected;
                _board.CheckerCaptured -= HandleCheckerCaptured;
                _board.CheckerDestroyed -= HandleCheckerDestroyed;
                _board.CheckerAdded -= HandleCheckerAdded;
                _board.CheckerTeleported -= HandleCheckerTeleported;
                _board.CheckerReleased -= HandleCheckerReleased;
                _board.CheckerSacrificed -= HandleCheckerSacrificed;
                _board.TileTriggered -= HandleTileTriggered;
                _board.AbilityTriggered -= HandleAbilityTriggered;
                _board.TurnChanged -= HandleTurnChanged;
                _board.TurnCountChanged -= HandleTurnCountChanged;
                _board.CheckerBorneOff -= HandleCheckerBorneOff;
                _board.FinalStretchStarted -= HandleFinalStretchStarted;
                _board.MatchEnded -= HandleMatchEnded;
                _board = null;
            }

            if (_context != null)
            {
                _context.DiceRolled -= HandleDiceRolled;
                _context.ScoreChanged -= HandleScoreChanged;
                _context.GoldChanged -= HandleGoldChanged;
                _context = null;
            }
        }

        #region GameState listeners

        private void HandleMoveExecuted(MoveResult result)
        {
            string suffix = result.WasInfiltration ? " [infiltration]" : string.Empty;
            string to = result.WasBearOff ? "bear-off" : result.ToIndex.ToString();
            GameLog.Info($"[View] Move: {result.FromIndex} -> {to} " +
                         $"(player {result.PlayerId}, captured: {result.CapturedCheckers.Count}){suffix}");
        }

        private void HandleMoveRejected(MoveResult result)
        {
            GameLog.Info($"[View] Move rejected: {result.Error}");
        }

        private void HandleCheckerCaptured(BaseChecker checker, int nodeIndex, CaptureDestination destination)
        {
            switch (destination)
            {
                case CaptureDestination.HomeEntry:
                    GameLog.Info($"[View] {checker.TypeId} rose from the ashes and returned to its starting point.");
                    break;
                case CaptureDestination.Destroyed:
                    GameLog.Info($"[View] {checker.TypeId} sacrificed itself.");
                    break;
                case CaptureDestination.Converted:
                    GameLog.Info($"[View] {checker.TypeId} mutinied and changed sides (point {nodeIndex}).");
                    break;
                default:
                    GameLog.Info($"[View] {checker.TypeId} was hit and sent to the bar (point {nodeIndex}).");
                    break;
            }
        }

        private void HandleCheckerDestroyed(BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} was removed from the game (point {nodeIndex}).");
        }

        private void HandleCheckerAdded(BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} joined player {checker.OwnerId} (point {nodeIndex}).");
        }

        private void HandleCheckerSacrificed(BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} was sacrificed on point {nodeIndex} for its ability.");
        }

        private void HandleCheckerTeleported(BaseChecker checker, int fromIndex, int toIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} was caught by the maelstrom: {fromIndex} -> {toIndex}");
        }

        private void HandleCheckerReleased(BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} was freed on point {nodeIndex} by sacrificing a die.");
        }

        private void HandleTileTriggered(ITileEffect tile, int nodeIndex, BaseChecker checker)
        {
            GameLog.Info($"[View] Tile triggered: {tile.EffectId} (point {nodeIndex})");
        }

        private void HandleAbilityTriggered(string abilityId, BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] Ability: {abilityId} (point {nodeIndex})");
        }

        private void HandleTurnChanged(int playerId)
        {
            GameLog.Info($"[View] Turn: player {playerId}");
        }

        private void HandleTurnCountChanged(int remaining)
        {
            GameLog.Info($"[View] Turns remaining: {remaining}");
        }

        private void HandleCheckerBorneOff(BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] Checker borne off: {checker.TypeId} (point {nodeIndex})");
        }

        private void HandleFinalStretchStarted(int remaining)
        {
            GameLog.Info($"[View] The opponent is out of checkers — final stretch, {remaining} turns left.");
        }

        private void HandleMatchEnded(int winnerId)
        {
            string message = winnerId == GameState.NoWinner
                ? "The match ended in a draw."
                : $"Match over — winner: player {winnerId}";

            GameLog.Info($"[View] {message} (P0: {_context.GetScore(0)} — P1: {_context.GetScore(1)})");
        }

        #endregion

        #region MatchContext listeners

        private void HandleDiceRolled(DiceRoll roll)
        {
            GameLog.Info($"[View] Dice: {roll.First}-{roll.Second}" + (roll.IsDouble ? " (double!)" : string.Empty));
        }

        private void HandleScoreChanged(int playerId, int score) => GameLog.Info($"[View] Score P{playerId}: {score}");

        private void HandleGoldChanged(int playerId, int gold) => GameLog.Info($"[View] Gold P{playerId}: {gold}");

        #endregion
    }
}

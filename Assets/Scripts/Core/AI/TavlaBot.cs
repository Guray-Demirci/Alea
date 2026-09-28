using System.Collections.Generic;
using Tavla.Core.Board;

namespace Tavla.Core.AI
{
    /// <summary>
    /// One-ply (greedy) AI.
    ///
    /// How it works: for every candidate move it takes a CLONE of the board, plays the move
    /// on the clone, scores the result and picks the highest-scoring move. The clone never
    /// affects the real board because <see cref="GameState.IsSimulation"/> disables side-effect hooks.
    ///
    /// There is NO rule knowledge here. "Is this move legal?" is always asked of Core
    /// (GetLegalTargets / CanBearOff / TryMove) — like the View, the bot is a consumer of the rules, not their owner.
    /// </summary>
    public class TavlaBot
    {
        #region Identity

        private readonly int _myPlayerId;
        private readonly int _opponentId;

        public int PlayerId => _myPlayerId;

        public TavlaBot(int myPlayerId)
        {
            _myPlayerId = myPlayerId;
            _opponentId = myPlayerId == 0 ? 1 : 0;
        }

        #endregion

        #region Weights

        // Tuning knobs of the heuristic. This is the single place to make the bot harder or softer.
        private const int BarPenalty        = 10000; // One of our checkers was hit — disaster
        private const int BarReward         = 1000;  // We hit an opponent checker
        private const int BorneOffReward    = 5000;  // We bore off a checker — the ultimate goal
        private const int BlotPenalty       = 300;   // We left a blot that can be hit
        private const int PointReward       = 200;   // We made a point and are safe
        private const int ProgressPerStep   = 10;    // Moving closer to home

        #endregion

        // Reused between calls so move selection does not allocate new lists every time.
        private readonly List<int> _sources = new List<int>(GameState.NodeCount);
        private readonly List<int> _targets = new List<int>(GameState.NodeCount);

        #region Evaluation

        /// <summary>
        /// The bot's brain. Looks at a board position and assigns a score.
        /// The higher the score, the better the position is for the bot.
        /// </summary>
        public int EvaluateBoard(GameState virtualBoard)
        {
            int score = 0;

            // ---------- 1) HITTING AND BEING HIT ----------
            score -= virtualBoard.GetBarCount(_myPlayerId) * BarPenalty;
            score += virtualBoard.GetBarCount(_opponentId) * BarReward;

            // ---------- 2) BORNE-OFF CHECKERS (the ultimate goal) ----------
            // The opponent's borne-off checkers count too: in arcade mode the winner is decided
            // by score, so the bot must not sit idle while the opponent bears off 14 checkers.
            score += virtualBoard.GetBorneOffCount(_myPlayerId) * BorneOffReward;
            score -= virtualBoard.GetBorneOffCount(_opponentId) * BorneOffReward;

            // ---------- 3) BOARD SHAPE (blots, points, progress) ----------
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                Node node = virtualBoard.Nodes[i];

                // NOTE: NOT "node.Checkers[0].OwnerId == me".
                // The Parasite Checker can infiltrate enemy points, so a point may hold checkers
                // of both players; counting must be done per player.
                int myCount = node.CountFor(_myPlayerId);
                if (myCount == 0)
                    continue;

                if (myCount == 1)
                    score -= BlotPenalty;   // Blot
                else
                    score += PointReward;   // Made point (blockade)

                // Progress: 1 = closest to finishing, 24 = furthest away.
                int distanceToFinish = GameState.GetBearOffDistance(i, _myPlayerId);

                // Multiplied by the checker COUNT: if a 5-checker stack counted the same as a
                // single checker, the bot would never want to move stacks forward.
                score += (GameState.NodeCount - distanceToFinish) * ProgressPerStep * myCount;
            }

            return score;
        }

        #endregion

        #region Move selection

        /// <summary>
        /// Tries every legal move and picks the highest-scoring one.
        /// Does NOT change the board — all attempts are made on clones.
        /// </summary>
        public BotMove ChooseBestMove(GameState board)
        {
            if (board == null || board.IsMatchOver || board.CurrentPlayerId != _myPlayerId)
                return BotMove.None;

            BotMove best = BotMove.None;
            int bestScore = int.MinValue;

            CollectSources(board, _sources);

            for (int s = 0; s < _sources.Count; s++)
            {
                int from = _sources[s];

                // --- Targets on the board ---
                board.GetLegalTargets(from, _targets);
                for (int t = 0; t < _targets.Count; t++)
                {
                    int value;
                    if (!TrySimulate(board, from, _targets[t], out value))
                        continue;

                    if (value > bestScore)
                    {
                        bestScore = value;
                        best = new BotMove(from, _targets[t], value);
                    }
                }

                // --- Bear-off ---
                // GetLegalTargets only scans points 0-23; bearing off must be queried separately.
                if (board.CanBearOff(from))
                {
                    int value;
                    if (TrySimulate(board, from, GameState.BearOffIndex, out value) && value > bestScore)
                    {
                        bestScore = value;
                        best = new BotMove(from, GameState.BearOffIndex, value);
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Plays the best move on the REAL board. Returns false when no move is left —
        /// the caller uses this as the "end the turn" signal.
        /// </summary>
        public bool PlayOneMove(GameState board)
        {
            BotMove move = ChooseBestMove(board);
            if (!move.IsValid)
                return false;

            return board.TryMove(move.FromIndex, move.ToIndex);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Source points from which a move can be played.
        /// If a checker is on the bar no other move is allowed — the bar is the only source.
        /// </summary>
        private void CollectSources(GameState board, List<int> sources)
        {
            sources.Clear();

            if (board.GetBarCount(_myPlayerId) > 0)
            {
                sources.Add(GameState.BarIndex);
                return;
            }

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                if (board.Nodes[i].CountFor(_myPlayerId) > 0)
                    sources.Add(i);
            }
        }

        /// <summary>
        /// Plays the move on a clone and scores the result. Returns false if the move fails on the clone.
        /// Never touches the real board, the real dice or the internal state of real checkers.
        /// </summary>
        private bool TrySimulate(GameState board, int from, int to, out int value)
        {
            value = int.MinValue;

            GameState virtualBoard = board.Clone();
            if (!virtualBoard.TryMove(from, to))
                return false;

            value = EvaluateBoard(virtualBoard);
            return true;
        }

        #endregion
    }
}

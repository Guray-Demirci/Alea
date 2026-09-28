using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// CORE -> HUD bridge. The only place that knows WHAT to write on the panel.
    ///
    /// Division of labour:
    ///   Core         : UNAWARE of the HUD, it only raises events.
    ///   HudPresenter : listens to those events and turns them into text (this file).
    ///   HudView      : knows where, in which colour and in which order to draw the text.
    ///
    /// So a change to the panel layout never touches Core, and a rule change never touches the panel.
    /// Same contract as BoardManager: there are NO game rules here, not a single line of calculation;
    /// every number shown is read from Core as is.
    ///
    /// The connection has two phases (the same pattern as MatchContext <-> GameState): BoardManager
    /// calls Bind after Core is up. The game still runs if this component is not in the scene.
    ///
    /// All player-facing strings are serialized fields so they can be changed (or later fed from a
    /// localization table) without touching code. Keep them within the PixelFont character set
    /// (A-Z, 0-9 and basic punctuation).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudPresenter : MonoBehaviour
    {
        [Header("Connection")]
        [Tooltip("If empty, it is looked up on the same object, then in the scene.")]
        [SerializeField] private HudView _view;

        [Header("Displayed player")]
        [Tooltip("Player whose score and gold the panel shows. There is one panel: if the bot is player 1, keep this at 0.")]
        [SerializeField] private int _playerId = 0;

        [Header("Formatting")]
        [Tooltip("Separator placed between the remaining dice.")]
        [SerializeField] private string _diceSeparator = " ";
        [Tooltip("Text shown when there is no value (before the dice are rolled, after all moves are spent).")]
        [SerializeField] private string _emptyValue = "-";
        [Tooltip("Prefix of the remaining-turns text on the task line. The number is appended.")]
        [SerializeField] private string _turnsRemainingPrefix = "TURN ";

        [Header("Match result")]
        [SerializeField] private string _winText = "YOU WIN";
        [SerializeField] private string _loseText = "YOU LOSE";
        [SerializeField] private string _drawText = "DRAW";

        private GameState _board;
        private MatchContext _context;

        // The dice list is rebuilt on every move; avoids string concatenation each time.
        private readonly StringBuilder _builder = new StringBuilder(16);

        private void Awake()
        {
            if (_view == null)
                _view = GetComponent<HudView>();

            if (_view == null)
                _view = FindAnyObjectByType<HudView>();

            if (_view == null)
                Debug.LogError("[View] HudPresenter could not find a HudView — the panel will stay empty. " +
                               "Add the component to the general_info_scheme prefab or assign the View field.", this);
        }

        #region Binding

        /// <summary>
        /// Connects to Core and syncs the panel with the current state.
        /// BoardManager calls this RIGHT AFTER GameState + MatchContext are created.
        /// </summary>
        public void Bind(GameState board, MatchContext context)
        {
            if (board == null || context == null)
            {
                Debug.LogError("[View] HudPresenter.Bind received a null Core; the panel will not update.", this);
                return;
            }

            // Binding twice must not make events fire twice.
            Unbind();

            _board = board;
            _context = context;

            _board.TurnChanged += HandleTurnChanged;
            _board.TurnCountChanged += HandleTurnCountChanged;
            _board.MoveExecuted += HandleMoveExecuted;
            _board.MatchEnded += HandleMatchEnded;

            _context.ScoreChanged += HandleScoreChanged;
            _context.GoldChanged += HandleGoldChanged;
            _context.DiceRolled += HandleDiceRolled;

            RefreshAll();
        }

        /// <summary>
        /// Removes the subscriptions. Same reason as BoardManager.OnDestroy: if Core keeps a reference to
        /// a destroyed MonoBehaviour, it leaks on scene changes.
        /// </summary>
        public void Unbind()
        {
            if (_board != null)
            {
                _board.TurnChanged -= HandleTurnChanged;
                _board.TurnCountChanged -= HandleTurnCountChanged;
                _board.MoveExecuted -= HandleMoveExecuted;
                _board.MatchEnded -= HandleMatchEnded;
                _board = null;
            }

            if (_context != null)
            {
                _context.ScoreChanged -= HandleScoreChanged;
                _context.GoldChanged -= HandleGoldChanged;
                _context.DiceRolled -= HandleDiceRolled;
                _context = null;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        #endregion

        #region Refresh

        /// <summary>
        /// Re-reads every slot from the model.
        ///
        /// Required in addition to the events: no event fires for the values present at bind time,
        /// so the panel would stay empty until the first dice roll.
        /// </summary>
        public void RefreshAll()
        {
            if (_view == null || _context == null || _board == null)
                return;

            _view.SetField(HudField.Score, _context.GetScore(_playerId));
            _view.SetField(HudField.Gold, _context.GetGold(_playerId));
            _view.SetField(HudField.Moves, FormatMoves());
            _view.SetField(HudField.Task, FormatTurns(_board.RemainingTurns));
        }

        /// <summary>
        /// Writes custom text on the task line — the hook for a future quest/contract system.
        /// Overrides the remaining-turns text.
        /// </summary>
        public void SetTask(string text)
        {
            if (_view != null)
                _view.SetField(HudField.Task, text);
        }

        private void RefreshMoves()
        {
            if (_view != null)
                _view.SetField(HudField.Moves, FormatMoves());
        }

        #endregion

        #region Core listeners

        private void HandleScoreChanged(int playerId, int score)
        {
            if (playerId == _playerId && _view != null)
                _view.SetField(HudField.Score, score);
        }

        private void HandleGoldChanged(int playerId, int gold)
        {
            if (playerId == _playerId && _view != null)
                _view.SetField(HudField.Gold, gold);
        }

        private void HandleDiceRolled(DiceRoll roll)
        {
            RefreshMoves();
        }

        /// <summary>A move spends a die; the remaining moves change.</summary>
        private void HandleMoveExecuted(MoveResult result)
        {
            RefreshMoves();
        }

        private void HandleTurnChanged(int playerId)
        {
            // The dice are NOT rolled YET (BoardManager rolls after taking the turn) — so the "-" shown
            // here is correct: the new turn's moves are not known yet.
            RefreshMoves();
        }

        private void HandleTurnCountChanged(int remaining)
        {
            if (_view != null)
                _view.SetField(HudField.Task, FormatTurns(remaining));
        }

        private void HandleMatchEnded(int winnerId)
        {
            if (_view == null)
                return;

            string message;
            if (winnerId == GameState.NoWinner)
                message = _drawText;
            else if (winnerId == _playerId)
                message = _winText;
            else
                message = _loseText;

            _view.SetField(HudField.Task, message);
            _view.SetField(HudField.Moves, _emptyValue);
        }

        #endregion

        #region Formatting

        /// <summary>
        /// Remaining moves. Both dice consumption modes are shown in the same slot:
        ///   Normal     : the remaining die values ("5 3", "6 6 6 6" on a double),
        ///   FreeSplit  : a single splittable pool ("8") — while Cartographer's Cheat is active.
        /// </summary>
        private string FormatMoves()
        {
            if (_context == null)
                return _emptyValue;

            DiceRoll roll = _context.CurrentRoll;
            if (roll == null || !roll.HasMovesLeft)
                return _emptyValue;

            if (roll.FreeSplit)
                return roll.FreeTotal.ToString();

            IReadOnlyList<int> moves = roll.RemainingMoves;

            _builder.Clear();
            for (int i = 0; i < moves.Count; i++)
            {
                if (i > 0)
                    _builder.Append(_diceSeparator);

                _builder.Append(moves[i]);
            }

            return _builder.ToString();
        }

        private string FormatTurns(int remaining)
        {
            return _turnsRemainingPrefix + remaining;
        }

        #endregion
    }
}

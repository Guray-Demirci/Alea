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
    /// (printable ASCII; Turkish letters are drawn without their marks).
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
        [Tooltip("Text shown when there is no value (a match without a target score).")]
        [SerializeField] private string _emptyValue = "-";
        [Tooltip("Prefix of the remaining-turns text on the status line. The number is appended.")]
        [SerializeField] private string _turnsRemainingPrefix = "TURN ";

        [Header("Match result")]
        [SerializeField] private string _winText = "YOU WIN";
        [SerializeField] private string _loseText = "YOU LOSE";
        [SerializeField] private string _drawText = "DRAW";

        private GameState _board;
        private MatchContext _context;

        private void Awake()
        {
            if (_view == null)
                _view = GetComponent<HudView>();

            if (_view == null)
                _view = FindAnyObjectByType<HudView>();

            if (_view == null)
                Debug.LogError("[View] HudPresenter could not find a HudView — the panel will stay empty. " +
                               "Add a HudView to the HUD object or assign the View field.", this);
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

            _board.TurnCountChanged += HandleTurnCountChanged;
            _board.MatchEnded += HandleMatchEnded;

            _context.ScoreChanged += HandleScoreChanged;
            _context.GoldChanged += HandleGoldChanged;

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
                _board.TurnCountChanged -= HandleTurnCountChanged;
                _board.MatchEnded -= HandleMatchEnded;
                _board = null;
            }

            if (_context != null)
            {
                _context.ScoreChanged -= HandleScoreChanged;
                _context.GoldChanged -= HandleGoldChanged;
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
        /// and the target score never changes during a match, so no event ever carries it.
        /// </summary>
        public void RefreshAll()
        {
            if (_view == null || _context == null || _board == null)
                return;

            _view.SetField(HudField.Score, _context.GetScore(_playerId));
            _view.SetField(HudField.Gold, _context.GetGold(_playerId));
            _view.SetField(HudField.Target, FormatTarget(_board.Rules));
            _view.SetField(HudField.Status, FormatTurns(_board.RemainingTurns));
        }

        /// <summary>
        /// Writes custom text on the status line (a volley's picks left, the shop).
        /// Overrides the remaining-turns text.
        /// </summary>
        public void SetStatus(string text)
        {
            if (_view != null)
                _view.SetField(HudField.Status, text);
        }

        /// <summary>
        /// Shows gold that is not the bound match's — the run's purse between matches, while the shop is open.
        /// The next <see cref="Bind"/> shows the match's gold again.
        /// </summary>
        public void ShowGold(int gold)
        {
            if (_view != null)
                _view.SetField(HudField.Gold, gold);
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

        private void HandleTurnCountChanged(int remaining)
        {
            if (_view != null)
                _view.SetField(HudField.Status, FormatTurns(remaining));
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

            _view.SetField(HudField.Status, message);
        }

        #endregion

        #region Formatting

        /// <summary>The score to reach. A classic match (no target, like most sandbox setups) shows the empty value.</summary>
        private string FormatTarget(MatchRules rules)
        {
            return rules.HasTargetScore ? rules.TargetScore.Value.ToString() : _emptyValue;
        }

        private string FormatTurns(int remaining)
        {
            return _turnsRemainingPrefix + remaining;
        }

        #endregion
    }
}

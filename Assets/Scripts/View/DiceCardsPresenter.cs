using System;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// CORE -> DICE CARDS bridge, the cards' counterpart of <see cref="HudPresenter"/>: it listens to the same
    /// events that change the dice and has <see cref="DiceCardsView"/> turn the cards to match. Which card shows
    /// what is <see cref="DiceCardFaces"/>' rule. Only some players' rolls turn the cards up — the owner decides whose
    /// (the human's, not the bot's); while any other player has the turn, the cards lie face down.
    ///
    /// BoardManager binds it to every match before the first roll, like the HUD. A turn change needs no handling of
    /// its own: BoardManager rolls inside it, and a new roll first lays down the cards left up by the last turn
    /// (<see cref="DiceCardsView.Deal"/>). Laying them down on TurnChanged would even undo that roll, since
    /// BoardManager hears the event first.
    /// </summary>
    public sealed class DiceCardsPresenter
    {
        private readonly DiceCardsView _view;
        private readonly Func<int, bool> _showsDiceOf;
        private readonly int[] _faces = new int[DiceCardFaces.CardCount];

        private GameState _board;
        private MatchContext _context;

        /// <param name="showsDiceOf">
        /// Does this player's roll turn the cards up? Asked whenever the cards are turned, so the answer may change
        /// mid-match; call <see cref="Refresh"/> when it does.
        /// </param>
        public DiceCardsPresenter(DiceCardsView view, Func<int, bool> showsDiceOf)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _showsDiceOf = showsDiceOf ?? throw new ArgumentNullException(nameof(showsDiceOf));
        }

        /// <summary>Connects to a match and shows its dice; the cards start face down.</summary>
        public void Bind(GameState board, MatchContext context)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Binding twice must not make events fire twice.
            Unbind();

            _board = board;
            _context = context;

            _board.MoveExecuted += HandleMoveExecuted;
            _board.CheckerReleased += HandleCheckerChanged;
            _board.CheckerSacrificed += HandleCheckerChanged;
            _board.MatchEnded += HandleMatchEnded;

            _context.DiceRolled += HandleDiceRolled;

            Refresh();
        }

        /// <summary>Lets go of the match and lays the cards face down. Safe to call more than once.</summary>
        public void Unbind()
        {
            if (_board != null)
            {
                _board.MoveExecuted -= HandleMoveExecuted;
                _board.CheckerReleased -= HandleCheckerChanged;
                _board.CheckerSacrificed -= HandleCheckerChanged;
                _board.MatchEnded -= HandleMatchEnded;
                _board = null;
            }

            if (_context != null)
            {
                _context.DiceRolled -= HandleDiceRolled;
                _context = null;
            }

            // During scene teardown the view may already be destroyed (Unity's == reports that as null).
            if (_view != null)
                _view.LayFaceDown();
        }

        /// <summary>Turns the cards to the dice in hand right now. Does nothing while no match is bound.</summary>
        public void Refresh()
        {
            if (_board == null)
                return;

            ComputeFaces(_context.CurrentRoll);
            _view.Show(_faces);
        }

        /// <summary>
        /// The faces for these dice — the dice of the player whose turn it is; all down if that player's dice are not
        /// shown, or once the match is over. The match can end inside a move whose MoveExecuted comes after
        /// MatchEnded, and the dice left in hand must not turn up again then.
        /// </summary>
        private void ComputeFaces(DiceRoll roll)
        {
            if (_board.IsMatchOver || !_showsDiceOf(_board.CurrentPlayerId))
                Array.Clear(_faces, 0, _faces.Length);
            else
                DiceCardFaces.Compute(roll, _faces);
        }

        private void HandleDiceRolled(DiceRoll roll)
        {
            ComputeFaces(roll);
            _view.Deal(_faces);
        }

        /// <summary>A move spends a die, and some abilities reroll the dice left (Dice Holder) or add one.</summary>
        private void HandleMoveExecuted(MoveResult result)
        {
            Refresh();
        }

        /// <summary>Freeing a locked checker spends a die; sacrificing one for its ability can add one.</summary>
        private void HandleCheckerChanged(BaseChecker checker, int nodeIndex)
        {
            Refresh();
        }

        /// <summary>Dice left unplayed when the match ends go down.</summary>
        private void HandleMatchEnded(int winnerId)
        {
            Refresh();
        }
    }
}

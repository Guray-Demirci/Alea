using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Swamp Tile. "Every checker that lands here gets stuck (locked).
    /// Unlocking it requires sacrificing one of the dice on a later turn."
    /// The GDD gives the lock a 50% chance; the user made it certain (2026-10-13).
    /// The lock is kept PER CHECKER: one checker on the point may be stuck while another is free.
    /// Freeing goes through <see cref="GameState.TryReleaseChecker"/>, which spends the die.
    /// </summary>
    public class SwampTile : BaseTileEffect, IReleasableLock
    {
        public const string Id = "tile_swamp";

        private readonly HashSet<BaseChecker> _stuck = new HashSet<BaseChecker>();

        // Checkers that sank during the turn being played. "On a later turn" means they cannot be
        // freed with the dice left over from the very turn they got stuck in.
        private readonly HashSet<BaseChecker> _stuckThisTurn = new HashSet<BaseChecker>();

        public override string EffectId => Id;

        public override bool CanLeaveFrom(BaseChecker checker) => !_stuck.Contains(checker);

        /// <summary>Is this checker locked here? For the View, to show the lock on the board.</summary>
        public bool IsStuck(BaseChecker checker) => _stuck.Contains(checker);

        public bool CanRelease(BaseChecker checker) => _stuck.Contains(checker) && !_stuckThisTurn.Contains(checker);

        public void Release(IMatchContext context, Node node, BaseChecker checker)
        {
            if (!_stuck.Remove(checker))
                return;

            context.Board.ReportAbility(AbilityIds.SwampReleased, checker, node.Index);
        }

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            _stuck.Add(checker);
            _stuckThisTurn.Add(checker);
            context.Board.ReportAbility(AbilityIds.SwampStuck, checker, node.Index);
        }

        public override void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker)
        {
            // Once the checker has left the point the lock record is no longer needed (also prevents leaks).
            _stuck.Remove(checker);
            _stuckThisTurn.Remove(checker);
        }

        public override void OnTurnPassed(IMatchContext context, Node node, int endingPlayerId)
        {
            // The turn they sank in is over; from now on a die can free them.
            _stuckThisTurn.Clear();
        }
    }
}

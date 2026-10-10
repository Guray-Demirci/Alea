using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Board
{
    // Swapping places with a friendly checker instead of moving forward (Voodoo).
    public partial class GameState
    {
        /// <summary>
        /// Voodoo Checker: goes back by a die while the friendly checker standing there comes forward to the
        /// Voodoo's point. ResolveMove hands over once the source, the owner and the source lock are checked;
        /// it pays with one die like any move.
        ///
        /// Neither point changes hands, so nothing is captured, nothing is passed over and the move reactors stay
        /// quiet — a Siege on the target would otherwise see a point "made" that was there all along. Both
        /// checkers do leave and land, so the tiles of both points have their say.
        /// </summary>
        /// <param name="distance">Points back to the partner (positive).</param>
        private MoveResult ResolveSwap(int fromIndex, int toIndex, int playerId, BaseChecker mover, int distance,
                                       bool commit)
        {
            if (distance > MaxDieValue)
                return MoveResult.Fail(MoveError.InvalidDistance, fromIndex, toIndex, playerId);

            if (!CanAffordDistance(distance))
                return MoveResult.Fail(MoveError.NoDiceValue, fromIndex, toIndex, playerId);

            Node source = _nodes[fromIndex];
            Node target = _nodes[toIndex];

            // A swap needs a friendly partner; without one this is just a move the wrong way.
            BaseChecker partner = PeekSource(toIndex, playerId);
            if (partner == null)
                return MoveResult.Fail(MoveError.WrongDirection, fromIndex, toIndex, playerId);

            // No swapping into a point the opponent shares (one of our Parasites infiltrated it).
            if (target.CountEnemiesOf(playerId) > 0)
                return MoveResult.Fail(MoveError.DestinationBlocked, fromIndex, toIndex, playerId);

            ITileEffect partnerSource = TileFor(target, partner);
            if (partnerSource != null && !partnerSource.CanLeaveFrom(partner))
                return MoveResult.Fail(MoveError.CheckerLocked, fromIndex, toIndex, playerId);

            ITileEffect moverTarget = TileFor(target, mover);
            ITileEffect partnerTarget = TileFor(source, partner);
            if ((moverTarget != null && !moverTarget.CanLandOn(mover)) ||
                (partnerTarget != null && !partnerTarget.CanLandOn(partner)))
                return MoveResult.Fail(MoveError.BlockedByTile, fromIndex, toIndex, playerId);

            if (!commit)
                return MoveResult.Ok(fromIndex, toIndex, distance, playerId, mover, null, false, 0);

            ConsumeDistance(distance);

            RemoveFromNode(source, mover);
            RemoveFromNode(target, partner);
            target.Push(mover);
            source.Push(partner);

            ReportAbility(AbilityIds.Swap, mover, toIndex);

            ApplyLandingTile(target, mover);
            ApplyLandingTile(source, partner);

            return MoveResult.Ok(fromIndex, toIndex, distance, playerId, mover, null, false, 0);
        }
    }
}

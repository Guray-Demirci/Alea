using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Board
{
    // Captures, forced relocation/destruction and the dispatch of ability/tile hooks.
    public partial class GameState
    {
        #region Hook dispatch

        /// <summary>The moving checker and the checkers already standing on the target point react.</summary>
        private void NotifyMoveReactors(Node target, BaseChecker mover, MoveResult result)
        {
            // Ability dispatch is skipped in simulations and when no context is bound.
            if (!SideEffectsEnabled)
                return;

            IMoveReactor moverReactor = mover.GetAbility<IMoveReactor>();
            if (moverReactor != null)
                moverReactor.OnMoveCompleted(Context, result, true);

            // Iterate over a copy: abilities may change the contents of the point.
            BaseChecker[] witnesses = new BaseChecker[target.CheckerCount];
            for (int i = 0; i < target.CheckerCount; i++)
                witnesses[i] = target.Checkers[i];

            for (int i = 0; i < witnesses.Length; i++)
            {
                if (ReferenceEquals(witnesses[i], mover))
                    continue;

                IMoveReactor reactor = witnesses[i].GetAbility<IMoveReactor>();
                if (reactor != null)
                    reactor.OnMoveCompleted(Context, result, false);
            }
        }

        /// <summary>Scans the points BETWEEN source and destination (both excluded).</summary>
        private void NotifyPassOver(int origin, int toIndex, int playerId, BaseChecker mover)
        {
            if (!SideEffectsEnabled)
                return;

            int step = GetDirection(playerId);

            for (int index = origin + step; index != toIndex; index += step)
            {
                if (!IsOnBoard(index))
                    continue;

                Node node = _nodes[index];

                if (node.ActiveTile != null)
                {
                    node.ActiveTile.OnCheckerPassedOver(Context, node, mover);
                    TileTriggered?.Invoke(node.ActiveTile, index, mover);
                }

                for (int i = node.CheckerCount - 1; i >= 0; i--)
                {
                    BaseChecker resident = node.Checkers[i];
                    if (resident.OwnerId == playerId)
                        continue;

                    IPassOverReactor reactor = resident.GetAbility<IPassOverReactor>();
                    if (reactor != null)
                        reactor.OnEnemyPassedOver(Context, mover, index);
                }
            }
        }

        private void NotifyTileLeft(Node source, BaseChecker checker)
        {
            if (!SideEffectsEnabled || source.ActiveTile == null)
                return;

            source.ActiveTile.OnCheckerLeft(Context, source, checker);

            if (source.ActiveTile.IsExpired)
                source.ClearTile();
        }

        /// <summary>
        /// The ONLY way to remove a checker from a point. Calling <see cref="Node.Remove"/> directly is
        /// NOT ALLOWED: tiles keep per-checker state (Fog ability lock, Swamp lock, Powder Keg fuse)
        /// and only OnCheckerLeft clears it. Any exit path that skips the hook leaves the checker
        /// fogged/stuck forever and accumulates dead references in the tile's dictionary.
        /// </summary>
        /// <returns>True if the checker really was on this point and has been removed.</returns>
        private bool RemoveFromNode(Node node, BaseChecker checker)
        {
            if (!node.Remove(checker))
                return false;

            NotifyTileLeft(node, checker);
            return true;
        }

        #endregion

        #region Captures

        /// <summary>
        /// The SINGLE application point of a capture. Tiles (Spike Pit, Maelstrom) and abilities
        /// (Bouncer) go through here too, so the Phoenix/Sacrificial rules work on every path.
        /// </summary>
        internal void ForceCapture(BaseChecker victim, int nodeIndex, int byPlayerId)
        {
            if (!IsOnBoard(nodeIndex))
                return;

            ApplyCapture(victim, _nodes[nodeIndex], byPlayerId);
        }

        /// <returns>Score earned by this capture (after multipliers).</returns>
        private int ApplyCapture(BaseChecker victim, Node node, int byPlayerId)
        {
            // RemoveFromNode is NOT used: it fires the hook immediately, while here the question
            // "where does it go?" must be answered between removal and the hook (see the note below).
            if (!node.Remove(victim))
                return 0;

            // The ability decides the destination: Phoenix -> starting point, Sacrificial -> destroyed.
            // Inside Fog GetAbility returns null, so the checker automatically goes to the bar.
            //
            // ORDER MATTERS: both the destination and the reaction are queried while the checker is STILL
            // under the tile's effect. The Fog rule says "a plain checker while it stands there"; if we
            // released the lock first, a Phoenix captured inside the Fog would go home instead of to the bar.
            ICustomCaptureDestination custom = victim.GetAbility<ICustomCaptureDestination>();
            CaptureDestination destination = custom != null ? custom.Destination : CaptureDestination.Bar;
            ICaptureReactor reactor = victim.GetAbility<ICaptureReactor>();

            // Decision made; the checker is now really leaving the point. If the capture path skipped
            // this hook, the Fog's ability lock would never be released and the checker would return
            // from the bar as a plain checker.
            NotifyTileLeft(node, victim);

            switch (destination)
            {
                case CaptureDestination.HomeEntry:
                    _nodes[GetHomeEntryIndex(victim.OwnerId)].Push(victim);
                    break;

                case CaptureDestination.Destroyed:
                    CheckerDestroyed?.Invoke(victim, node.Index);
                    break;

                default:
                    _bar[victim.OwnerId].Add(victim);
                    break;
            }

            // Checker objects are SHARED with clones, so this event is not raised in a simulation.
            if (SideEffectsEnabled)
                victim.OnCaptured(byPlayerId);

            // The reaction was resolved above, while the checker was still under the tile.
            if (reactor != null && SideEffectsEnabled)
                reactor.OnCaptured(Context, byPlayerId, node.Index);

            // AGGRESSIVE SCORING — only capturing an OPPONENT checker scores.
            // Without this condition the Bouncer's 25% self-hit would score for its own owner.
            // Neutral tiles such as Spike Pit/Maelstrom call this with the opponent's id, so a checker
            // they kill also scores for the opponent — a deliberate arcade decision.
            int scored = 0;
            if (byPlayerId != victim.OwnerId)
                scored = AwardScore(byPlayerId, CaptureScore);

            CheckerCaptured?.Invoke(victim, node.Index, destination);

            // A destroyed checker leaves play for good; it may have been its owner's last one.
            if (destination == CaptureDestination.Destroyed)
                HandleCheckerLeftPlay(victim.OwnerId);

            return scored;
        }

        /// <summary>
        /// Writes the score to MatchContext and returns the amount actually written (with multipliers).
        /// Score lives in ONE place — GameState keeps no copy of its own, so two counters can never diverge.
        /// </summary>
        private int AwardScore(int playerId, int rawAmount)
        {
            return Context != null ? Context.AddScore(playerId, rawAmount) : 0;
        }

        #endregion

        #region Forced relocation / destruction

        /// <summary>For effects that forcibly move a checker, such as the Maelstrom tile.</summary>
        internal void TeleportChecker(BaseChecker checker, int fromIndex, int toIndex)
        {
            if (!IsOnBoard(fromIndex) || !IsOnBoard(toIndex))
                return;

            // The tile must see the "checker left" hook: a checker dragged out of the Fog must regain
            // its abilities, and the Swamp must forget its lock record.
            if (!RemoveFromNode(_nodes[fromIndex], checker))
                return;

            _nodes[toIndex].Push(checker);
            CheckerTeleported?.Invoke(checker, fromIndex, toIndex);

            if (SideEffectsEnabled && _nodes[toIndex].ActiveTile != null)
                _nodes[toIndex].ActiveTile.OnCheckerLanded(Context, _nodes[toIndex], checker);
        }

        /// <summary>For effects that remove a checker from the game entirely, such as the Powder Keg.</summary>
        internal void DestroyChecker(BaseChecker checker, int nodeIndex)
        {
            // The hook must run even when the checker is destroyed: otherwise per-checker tile records
            // (Swamp lock, Powder Keg fuse) would keep dead references.
            if (!IsOnBoard(nodeIndex) || !RemoveFromNode(_nodes[nodeIndex], checker))
                return;

            CheckerDestroyed?.Invoke(checker, nodeIndex);
            HandleCheckerLeftPlay(checker.OwnerId);
        }

        #endregion
    }
}

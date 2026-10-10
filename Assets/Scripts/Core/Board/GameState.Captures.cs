using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
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

        /// <summary>The checker that was just borne off reacts (Golden). Bear-off has no target point, so no witnesses.</summary>
        private void NotifyBearOffReactor(BaseChecker mover, MoveResult result)
        {
            if (!SideEffectsEnabled)
                return;

            IBearOffReactor reactor = mover.GetAbility<IBearOffReactor>();
            if (reactor != null)
                reactor.OnBorneOff(Context, result);
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

                ITileEffect tile = TileFor(node, mover);
                if (tile != null)
                {
                    tile.OnCheckerPassedOver(Context, node, mover);
                    TileTriggered?.Invoke(tile, index, mover);
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
            if (!SideEffectsEnabled)
                return;

            ITileEffect tile = TileFor(source, checker);
            if (tile == null)
                return;

            tile.OnCheckerLeft(Context, source, checker);

            if (tile.IsExpired)
                source.ClearTile();
        }

        /// <summary>
        /// The tile of the point acts on a checker that has just settled there (Spike Pit, Maelstrom, Fog, Swamp...).
        /// A checker immune to tiles (Reckless) is only reported, so the View can show it shrugging the tile off.
        /// </summary>
        private void ApplyLandingTile(Node node, BaseChecker checker)
        {
            if (!SideEffectsEnabled || node.ActiveTile == null)
                return;

            ITileEffect tile = TileFor(node, checker);
            if (tile == null)
            {
                ReportAbility(AbilityIds.TileIgnored, checker, node.Index);
                return;
            }

            tile.OnCheckerLanded(Context, node, checker);
            TileTriggered?.Invoke(tile, node.Index, checker);

            if (tile.IsExpired)
                node.ClearTile();
        }

        /// <summary>
        /// The tile of the point as far as this checker is concerned: null when there is none, or when the checker
        /// is immune to tiles (Reckless). Every per-checker tile hook goes through here, so no tile has to know
        /// about immunity.
        /// </summary>
        private static ITileEffect TileFor(Node node, BaseChecker checker)
        {
            return node.ActiveTile != null && checker.IsAffectedByTiles ? node.ActiveTile : null;
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
        /// (Clover) go through here too, so the Phoenix/Sacrificial rules work on every path.
        /// No checker landed on the victim here, so no checker's score modifier applies.
        /// </summary>
        internal void ForceCapture(BaseChecker victim, int nodeIndex, int byPlayerId)
        {
            if (!IsOnBoard(nodeIndex))
                return;

            bool retaliates;
            ApplyCapture(victim, _nodes[nodeIndex], byPlayerId, null, out retaliates);
        }

        /// <param name="capturer">
        /// The checker whose move made the capture — still in flight, off its old point and not yet on this one.
        /// Null when a tile or another ability forced the capture.
        /// </param>
        /// <param name="retaliates">
        /// Does the victim take its capturer down with it (Gunpowder)? Only a capture with a capturer can;
        /// ResolveMove carries it out once the capturer has landed.
        /// </param>
        /// <returns>Score earned by this capture.</returns>
        private int ApplyCapture(BaseChecker victim, Node node, int byPlayerId, BaseChecker capturer, out bool retaliates)
        {
            retaliates = false;

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
            retaliates = capturer != null && victim.GetAbility<ICaptureRetaliation>() != null;

            // Mutiny Checker: a victim bound for the bar changes sides instead, if the capturer's side has room.
            BaseChecker convert = destination == CaptureDestination.Bar ? TryConvert(capturer, victim, node) : null;
            if (convert != null)
                destination = CaptureDestination.Converted;

            // Decision made; the checker is now really leaving the point. If the capture path skipped
            // this hook, the Fog's ability lock would never be released and the checker would return
            // from the bar as a plain checker.
            NotifyTileLeft(node, victim);

            // Phoenix: its starting point, or the first point after it with room. No room anywhere -> the bar.
            int homeIndex = InvalidIndex;
            if (destination == CaptureDestination.HomeEntry)
            {
                homeIndex = FindHomeLanding(victim.OwnerId, node, capturer);
                if (homeIndex == InvalidIndex)
                    destination = CaptureDestination.Bar;
            }

            switch (destination)
            {
                case CaptureDestination.HomeEntry:
                    _nodes[homeIndex].Push(victim);
                    break;

                case CaptureDestination.Destroyed:
                    CheckerDestroyed?.Invoke(victim, node.Index);
                    break;

                case CaptureDestination.Converted:
                    // The victim leaves play; its replacement takes its place, under the capturer about to land.
                    CheckerDestroyed?.Invoke(victim, node.Index);
                    node.Push(convert);
                    CheckerAdded?.Invoke(convert, node.Index);
                    ReportAbility(AbilityIds.CaptureConverted, convert, node.Index);
                    break;

                default:
                    _bar[victim.OwnerId].Add(victim);
                    break;
            }

            // The reaction was resolved above, while the checker was still under the tile. Checker objects are
            // SHARED with clones, so it does not run in a simulation.
            if (reactor != null && SideEffectsEnabled)
                reactor.OnCaptured(Context, byPlayerId, node.Index);

            // AGGRESSIVE SCORING — only capturing an OPPONENT checker scores.
            // Without this condition the Clover's self-hit would score for its own owner.
            // Neutral tiles such as Spike Pit/Maelstrom call this with the opponent's id, so a checker
            // they kill also scores for the opponent — a deliberate arcade decision.
            int scored = 0;
            if (byPlayerId != victim.OwnerId)
                scored = AwardScore(byPlayerId, ScoreSource.Capture, capturer, node.Index);

            CheckerCaptured?.Invoke(victim, node.Index, destination);

            // A destroyed or converted checker leaves play for good; it may have been its owner's last one.
            if (destination == CaptureDestination.Destroyed || destination == CaptureDestination.Converted)
                HandleCheckerLeftPlay(victim.OwnerId);

            return scored;
        }

        /// <summary>
        /// Phoenix Checker: the point it returns to — its starting point, or, if that one is full
        /// (<see cref="StackLimit"/>), the first point after it in its own direction with room.
        /// <see cref="InvalidIndex"/> if every point is full.
        /// </summary>
        /// <param name="captureNode">The point of the capture; the capturer in flight is about to land on it.</param>
        private int FindHomeLanding(int playerId, Node captureNode, BaseChecker capturer)
        {
            int direction = GetDirection(playerId);
            for (int index = GetHomeEntryIndex(playerId); IsOnBoard(index); index += direction)
            {
                int occupied = _nodes[index].CheckerCount;
                if (capturer != null && index == captureNode.Index)
                    occupied++;

                if (occupied < StackLimit)
                    return index;
            }

            return InvalidIndex;
        }

        /// <summary>
        /// Mutiny Checker: the checker that takes a captured victim's place, or null if the capturer converts
        /// nothing or its side has no room. The side may not field more than <see cref="MaxCheckersPerPlayer"/>
        /// checkers, and the point must hold both the new checker and the capturer about to land on it — the
        /// capturer is in flight, so both limits count it by hand.
        /// </summary>
        private BaseChecker TryConvert(BaseChecker capturer, BaseChecker victim, Node node)
        {
            ICaptureConverter converter = capturer != null ? capturer.GetAbility<ICaptureConverter>() : null;
            if (converter == null)
                return null;

            int owner = capturer.OwnerId;
            if (CountInPlay(owner) + 2 > MaxCheckersPerPlayer || node.CheckerCount + 2 > StackLimit)
                return null;

            BaseChecker convert = converter.Convert(victim);
            return convert != null && convert.OwnerId == owner ? convert : null;
        }

        /// <summary>What a rule-engine score was earned for.</summary>
        private enum ScoreSource
        {
            Capture,
            BearOff
        }

        /// <summary>
        /// Writes the score to MatchContext and returns the amount actually written.
        /// Score lives in ONE place — GameState keeps no copy of its own, so two counters can never diverge.
        ///
        /// The RAW amount is <see cref="CaptureScore"/> or <see cref="BearOffScore"/>, unless the player has marked the
        /// point (<see cref="ScoreMark"/>, Navigator footprints). The earner's own <see cref="IScoreModifier"/>
        /// (Catalyst) then shapes it before it is written.
        /// </summary>
        /// <param name="earner">Checker whose capture or bear-off earned the score; null if no checker did.</param>
        /// <param name="nodeIndex">Where it happened: the capture's point or the bear-off's source point.</param>
        private int AwardScore(int playerId, ScoreSource source, BaseChecker earner, int nodeIndex)
        {
            if (Context == null)
                return 0;

            ScoreMark mark = IsOnBoard(nodeIndex) ? _nodes[nodeIndex].GetMark(playerId) : null;
            int rawAmount = source == ScoreSource.Capture
                ? (mark != null ? mark.CaptureScore : CaptureScore)
                : (mark != null ? mark.BearOffScore : BearOffScore);

            IScoreModifier modifier = earner != null ? earner.GetAbility<IScoreModifier>() : null;
            if (modifier != null)
            {
                int modified = modifier.ModifyScore(rawAmount);
                if (modified != rawAmount)
                {
                    rawAmount = modified;
                    ReportAbility(AbilityIds.ScoreModified, earner, nodeIndex);
                }
            }

            return Context.AddScore(playerId, rawAmount);
        }

        /// <summary>
        /// A checker that marks the points it lands on (<see cref="IPointMarker"/>, Navigator) leaves its mark on the
        /// point it is landing on. Called before the landing's captures are scored, so they already count as made on
        /// the mark. The mark stays for the rest of the match; only a new one is reported.
        /// </summary>
        private void LeaveLandingMark(BaseChecker mover, int toIndex)
        {
            IPointMarker marker = mover.GetAbility<IPointMarker>();
            if (marker == null)
                return;

            Node node = _nodes[toIndex];
            if (ReferenceEquals(node.GetMark(mover.OwnerId), marker.LandingMark))
                return;

            node.SetMark(mover.OwnerId, marker.LandingMark);
            ReportAbility(AbilityIds.PointMarked, mover, toIndex);
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

            ITileEffect tile = TileFor(_nodes[toIndex], checker);
            if (SideEffectsEnabled && tile != null)
                tile.OnCheckerLanded(Context, _nodes[toIndex], checker);
        }

        /// <summary>
        /// For effects that destroy a checker for good — out of the match and, in a run, out of the player's deck —
        /// such as the Powder Keg.
        /// </summary>
        internal void DestroyChecker(BaseChecker checker, int nodeIndex)
        {
            // The hook must run even when the checker is destroyed: otherwise per-checker tile records
            // (Swamp lock, Powder Keg fuse) would keep dead references.
            if (!IsOnBoard(nodeIndex) || !RemoveFromNode(_nodes[nodeIndex], checker))
                return;

            CheckerDestroyed?.Invoke(checker, nodeIndex);
            CheckerLostForGood?.Invoke(checker, nodeIndex);
            HandleCheckerLeftPlay(checker.OwnerId);
        }

        #endregion
    }
}

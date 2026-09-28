using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Board
{
    // Public move API and single-move resolution.
    public partial class GameState
    {
        #region Move API

        /// <summary>
        /// The ONLY entry point the View calls.
        ///
        /// Two stages: first a direct single-die move is tried. If no single die covers the distance
        /// (e.g. moving 5 points while holding 3-2), a path that combines dice is searched.
        /// </summary>
        public bool TryMove(int fromIndex, int toIndex)
        {
            MoveResult result = ResolveMove(fromIndex, toIndex, CurrentPlayerId, true);

            if (result.Success)
            {
                MoveExecuted?.Invoke(result);
                return true;
            }

            // Compound moves are only tried for "not enough dice" errors.
            // Both checks happen in ResolveMove BEFORE any destination check, so no side effect
            // has occurred when we get here (no shield was spent).
            if (result.Error == MoveError.NoDiceValue || result.Error == MoveError.InvalidDistance)
            {
                if (TryCompoundMove(fromIndex, toIndex, CurrentPlayerId))
                    return true;
            }

            MoveRejected?.Invoke(result);
            return false;
        }

        /// <summary>
        /// Is the move legal? Does NOT change the board, raise events or spend shields.
        /// The View uses it to highlight "where can I go?" — the rule itself stays in Core,
        /// the View only paints the answer.
        /// </summary>
        public bool CanMove(int fromIndex, int toIndex)
        {
            return ResolveMove(fromIndex, toIndex, CurrentPlayerId, false).Success;
        }

        /// <summary>
        /// Can this destination be reached with a SINGLE die or by combining dice?
        /// Used for the View's highlight list — with 3-2 in hand the player also sees
        /// the point 5 away lit up.
        /// </summary>
        public bool CanReach(int fromIndex, int toIndex)
        {
            if (CanMove(fromIndex, toIndex))
                return true;

            return CanPlanCompound(fromIndex, toIndex, CurrentPlayerId);
        }

        /// <summary>All destinations reachable from the given point, for the current player.</summary>
        public List<int> GetLegalTargets(int fromIndex)
        {
            List<int> targets = new List<int>();
            GetLegalTargets(fromIndex, targets);
            return targets;
        }

        /// <summary>
        /// Non-allocating overload: clears <paramref name="results"/> and fills it with every
        /// destination reachable from the given point. Prefer this in per-frame or per-move code.
        /// </summary>
        public void GetLegalTargets(int fromIndex, List<int> results)
        {
            results.Clear();

            for (int to = 0; to < NodeCount; to++)
            {
                if (to != fromIndex && CanReach(fromIndex, to))
                    results.Add(to);
            }
        }

        /// <summary>
        /// Does the current player have at least one playable move? Does NOT touch the board.
        /// The View uses it to decide "dice spent / no move left, pass the turn".
        /// Source selection matches TavlaBot: if a checker is on the bar, the bar is the only source.
        /// </summary>
        public bool HasAnyLegalMove()
        {
            if (IsMatchOver)
                return false;

            // Cheap short circuit: no need for a 24x24 scan if all dice are spent.
            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            if (roll != null && !roll.HasMovesLeft)
                return false;

            int player = CurrentPlayerId;

            if (GetBarCount(player) > 0)
                return HasAnyTarget(BarIndex);

            for (int i = 0; i < NodeCount; i++)
            {
                if (_nodes[i].CountFor(player) == 0)
                    continue;

                if (CanBearOff(i) || HasAnyTarget(i))
                    return true;
            }

            return false;
        }

        /// <summary>Allocation-free variant of GetLegalTargets that stops at the first hit.</summary>
        private bool HasAnyTarget(int fromIndex)
        {
            for (int to = 0; to < NodeCount; to++)
            {
                if (to != fromIndex && CanReach(fromIndex, to))
                    return true;
            }

            return false;
        }

        #endregion

        #region Move resolution

        private MoveResult ResolveMove(int fromIndex, int toIndex, int playerId, bool commit)
        {
            // ---------- 0) Time bomb: once the match is closed no move is accepted ----------
            if (IsMatchOver)
                return MoveResult.Fail(MoveError.MatchOver, fromIndex, toIndex, playerId);

            // Bearing off is a separate path: the destination is not a point but the area off the board.
            // It goes through the same ResolveMove gate so both TryMove and CanMove support it.
            if (toIndex == BearOffIndex)
                return ResolveBearOff(fromIndex, playerId, commit);

            // ---------- 1) Basic validation ----------
            if (!IsOnBoard(toIndex) || (!IsOnBoard(fromIndex) && fromIndex != BarIndex))
                return MoveResult.Fail(MoveError.OutOfBounds, fromIndex, toIndex, playerId);

            if (GetBarCount(playerId) > 0 && fromIndex != BarIndex)
                return MoveResult.Fail(MoveError.MustEnterFromBar, fromIndex, toIndex, playerId);

            BaseChecker mover = PeekSource(fromIndex, playerId);
            if (mover == null)
                return MoveResult.Fail(MoveError.SourceEmpty, fromIndex, toIndex, playerId);

            if (mover.OwnerId != playerId)
                return MoveResult.Fail(MoveError.NotYourChecker, fromIndex, toIndex, playerId);

            // ---------- 2) Permission to leave (Swamp lock / Dungeon bars) ----------
            if (IsOnBoard(fromIndex))
            {
                ITileEffect sourceTile = _nodes[fromIndex].ActiveTile;
                if (sourceTile != null && !sourceTile.CanLeaveFrom(mover))
                    return MoveResult.Fail(MoveError.CheckerLocked, fromIndex, toIndex, playerId);
            }

            // ---------- 3) Direction and distance ----------
            int origin = fromIndex == BarIndex ? GetBarOrigin(playerId) : fromIndex;
            int distance = (toIndex - origin) * GetDirection(playerId);

            if (distance <= 0)
                return MoveResult.Fail(MoveError.WrongDirection, fromIndex, toIndex, playerId);

            // Nomad Checker: extra points when its neighbours are empty. The bonus is SUBTRACTED
            // from the required die, so the player can move 6 points with a 3 but gains no extra move.
            int requiredDie = distance;
            IExtraDistanceProvider extra = mover.GetAbility<IExtraDistanceProvider>();
            if (extra != null && !CanAffordDistance(distance))
            {
                int bonus = extra.GetBonusDistance(this, fromIndex);
                if (bonus > 0 && CanAffordDistance(distance - bonus))
                {
                    requiredDie = distance - bonus;
                    if (commit)
                        ReportAbility(AbilityIds.ExtraDistance, mover, toIndex);
                }
            }

            if (requiredDie <= 0 || requiredDie > MaxDieValue)
                return MoveResult.Fail(MoveError.InvalidDistance, fromIndex, toIndex, playerId);

            if (!CanAffordDistance(requiredDie))
                return MoveResult.Fail(MoveError.NoDiceValue, fromIndex, toIndex, playerId);

            // ---------- 4) Destination: tile permission ----------
            Node target = _nodes[toIndex];

            if (target.ActiveTile != null && !target.ActiveTile.CanLandOn(mover))
                return MoveResult.Fail(MoveError.BlockedByTile, fromIndex, toIndex, playerId);

            // ---------- 5) Destination: enemy checkers ----------
            List<BaseChecker> captures = null;
            bool infiltration = false;

            if (target.CountEnemiesOf(playerId) > 0)
            {
                IAreaCapture area = mover.GetAbility<IAreaCapture>();
                IInfiltrator infiltrator = mover.GetAbility<IInfiltrator>();
                IBlockadeBypass bypass = mover.GetAbility<IBlockadeBypass>();

                bool bypassAllowed = bypass != null && bypass.CanBypassBlockade(target);

                if (area != null && area.CanCaptureEntirePoint(target))
                {
                    // Doomsday Checker: sweeps the whole point.
                    captures = target.GetEnemiesOf(playerId);
                    if (commit)
                        ReportAbility(AbilityIds.AreaCapture, mover, toIndex);
                }
                else if (infiltrator != null && infiltrator.CanInfiltrate(target))
                {
                    // Parasite Checker: settles on the point next to the enemy without capturing.
                    infiltration = true;
                    if (commit)
                        ReportAbility(AbilityIds.Infiltrate, mover, toIndex);
                }
                else
                {
                    BaseChecker victim = target.GetTopEnemyOf(playerId);
                    bool singleEnemy = target.CountEnemiesOf(playerId) == 1;

                    if (singleEnemy && victim.CanBeCaptured && !target.IsBlockedFor(playerId))
                    {
                        captures = new List<BaseChecker> { victim };
                    }
                    else if (bypassAllowed)
                    {
                        // Ghost Checker: ignores the blockade and lands next to the enemy without capturing.
                        if (commit)
                            ReportAbility(AbilityIds.BlockadeBypass, mover, toIndex);
                    }
                    else
                    {
                        // If an immunity deflected the attack, notify the ability (for single-use shields).
                        ICaptureImmunity immunity = victim.GetAbility<ICaptureImmunity>();
                        // Shields are spent ONLY on a real attempt; dry queries never touch them.
                        if (commit && SideEffectsEnabled && immunity != null && immunity.IsImmune)
                        {
                            immunity.OnImmunityTriggered();
                            ReportAbility(AbilityIds.ImmunityBlocked, victim, toIndex);
                        }

                        return MoveResult.Fail(MoveError.DestinationBlocked, fromIndex, toIndex, playerId);
                    }
                }
            }

            // ---------- 6) Capacity (the Vortex Checker lifts this limit) ----------
            int stackLimit = target.GetStackLimit(DefaultStackLimit);
            if (target.CountFor(playerId) + 1 > stackLimit)
                return MoveResult.Fail(MoveError.StackFull, fromIndex, toIndex, playerId);

            // ===================================================================
            // From here on every rule has passed and the game state changes.
            // ===================================================================

            // A dry query (CanMove / GetLegalTargets) stops exactly here: it runs the SAME validation
            // code but never touches the board. Because the rules are not written twice, a
            // "highlighted point is legal but the move is rejected" mismatch is impossible.
            if (!commit)
                return MoveResult.Ok(fromIndex, toIndex, playerId, mover, captures, infiltration, 0);

            ConsumeDistance(requiredDie);

            if (IsOnBoard(fromIndex))
            {
                RemoveFromNode(_nodes[fromIndex], mover);
            }
            else
            {
                _bar[playerId].Remove(mover);
            }

            // Notify checkers/tiles along the path (Tax Checker, Toll Booth).
            NotifyPassOver(origin, toIndex, playerId, mover);

            // Aggressive scoring: every captured enemy checker scores.
            // ApplyCapture returns the awarded amount, we only sum it up.
            int scoreGained = 0;
            if (captures != null)
            {
                for (int i = 0; i < captures.Count; i++)
                    scoreGained += ApplyCapture(captures[i], target, playerId);
            }

            target.Push(mover);

            MoveResult result = MoveResult.Ok(fromIndex, toIndex, playerId, mover, captures,
                                              infiltration, scoreGained);

            // Tile effects apply AFTER the checker has settled (Spike Pit, Maelstrom, Fog, Swamp...).
            if (SideEffectsEnabled && target.ActiveTile != null)
            {
                ITileEffect tile = target.ActiveTile;
                tile.OnCheckerLanded(Context, target, mover);
                TileTriggered?.Invoke(tile, toIndex, mover);

                if (tile.IsExpired)
                    target.ClearTile();
            }

            NotifyMoveReactors(target, mover, result);

            return result;
        }

        private BaseChecker PeekSource(int fromIndex, int playerId)
        {
            if (fromIndex == BarIndex)
                return _bar[playerId].Count > 0 ? _bar[playerId][_bar[playerId].Count - 1] : null;

            // The point may also hold enemy checkers (Parasite), so look for OUR topmost checker.
            Node node = _nodes[fromIndex];
            for (int i = node.CheckerCount - 1; i >= 0; i--)
            {
                if (node.Checkers[i].OwnerId == playerId)
                    return node.Checkers[i];
            }
            return null;
        }

        #endregion
    }
}

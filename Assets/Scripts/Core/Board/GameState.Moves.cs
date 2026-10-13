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
        /// <summary>How a move is paid for.</summary>
        private enum MovePayment
        {
            /// <summary>One die covering the distance; the Nomad's bonus may cover part of it.</summary>
            Die,

            /// <summary>Exactly the die the distance names, no bonus — a planned step of a compound move.</summary>
            ExactDie,

            /// <summary>Every die left, whatever the distance — only a free mover (Teleporter) may.</summary>
            AllDice
        }

        #region Move API

        /// <summary>
        /// Moves the current player's topmost checker on the source point (the last one on the bar).
        /// The bot and rule tests play by point; the View names the checker the player picked up instead
        /// (<see cref="TryMove(BaseChecker, int)"/>).
        /// </summary>
        public bool TryMove(int fromIndex, int toIndex)
        {
            return TryMove(fromIndex, toIndex, null);
        }

        /// <summary>
        /// Moves THIS checker — the one the player picked up, wherever it sits in its stack. Special checkers
        /// share points with plain ones, so "the topmost one" is not necessarily the one the player meant.
        /// </summary>
        public bool TryMove(BaseChecker checker, int toIndex)
        {
            return checker != null && TryMove(FindNodeIndexOf(checker), toIndex, checker);
        }

        /// <summary>
        /// Three stages: first a direct single-die move is tried. If no single die covers the distance
        /// (e.g. moving 5 points while holding 3-2), a path that combines dice is searched. If none exists
        /// either, a free mover (Teleporter) may still go there for every die left.
        /// </summary>
        /// <param name="chosen">The checker to move; null for the topmost one on the source.</param>
        private bool TryMove(int fromIndex, int toIndex, BaseChecker chosen)
        {
            MoveResult result = ResolveMove(fromIndex, toIndex, CurrentPlayerId, true, MovePayment.Die, chosen);

            if (result.Success)
            {
                MoveExecuted?.Invoke(result);
                return true;
            }

            // Compound and free moves are only tried for "not enough dice" errors.
            // Both checks happen in ResolveMove BEFORE any destination check, so no side effect
            // has occurred when we get here (no shield was spent).
            if (result.Error == MoveError.NoDiceValue || result.Error == MoveError.InvalidDistance)
            {
                if (TryCompoundMove(fromIndex, toIndex, CurrentPlayerId, chosen))
                    return true;

                // No die and no combination of dice reaches the point. A free mover (Teleporter) still may,
                // at the cost of every die left.
                MoveResult free = ResolveMove(fromIndex, toIndex, CurrentPlayerId, true, MovePayment.AllDice, chosen);
                if (free.Success)
                {
                    MoveExecuted?.Invoke(free);
                    return true;
                }
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
        /// Can this destination be reached with a SINGLE die, by combining dice or — for a free mover — with all
        /// of them? Used for the View's highlight list — with 3-2 in hand the player also sees
        /// the point 5 away lit up.
        /// </summary>
        public bool CanReach(int fromIndex, int toIndex)
        {
            return CanReach(fromIndex, toIndex, null);
        }

        /// <param name="chosen">The checker to move; null for the topmost one on the source.</param>
        private bool CanReach(int fromIndex, int toIndex, BaseChecker chosen)
        {
            int player = CurrentPlayerId;

            return ResolveMove(fromIndex, toIndex, player, false, MovePayment.Die, chosen).Success
                || CanPlanCompound(fromIndex, toIndex, player, chosen)
                || ResolveMove(fromIndex, toIndex, player, false, MovePayment.AllDice, chosen).Success;
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
            GetLegalTargets(fromIndex, null, results);
        }

        /// <summary>
        /// Every destination THIS checker can reach, wherever it sits in its stack — the View's highlight for
        /// the checker the player picked up. Clears <paramref name="results"/> first.
        /// </summary>
        public void GetLegalTargets(BaseChecker checker, List<int> results)
        {
            if (checker == null)
            {
                results.Clear();
                return;
            }

            GetLegalTargets(FindNodeIndexOf(checker), checker, results);
        }

        private void GetLegalTargets(int fromIndex, BaseChecker chosen, List<int> results)
        {
            results.Clear();

            for (int to = 0; to < NodeCount; to++)
            {
                if (to != fromIndex && CanReach(fromIndex, to, chosen))
                    results.Add(to);
            }
        }

        /// <summary>
        /// Does the current player have at least one playable move? Does NOT touch the board.
        /// The View uses it to decide "dice spent / no move left, pass the turn".
        /// Every checker counts, not only the topmost ones: a buried Nomad may reach a point the checker
        /// above it cannot. So does a Bowman's volley waiting for its targets: the turn must not pass before it is fired.
        /// </summary>
        public bool HasAnyLegalMove()
        {
            if (HasPendingVolley)
                return true;

            if (GetMovableChecker(BarIndex) != null)
                return true;

            for (int i = 0; i < NodeCount; i++)
            {
                if (GetMovableChecker(i) != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The topmost of the current player's checkers on this source (a point, which enemies may share, or
        /// the bar) that can be played right now, or null if none can. Does NOT touch the board.
        ///
        /// Source selection matches TavlaBot: if a checker is on the bar, the bar is the only source.
        /// </summary>
        public BaseChecker GetMovableChecker(int fromIndex)
        {
            IReadOnlyList<BaseChecker> stack = fromIndex == BarIndex ? _bar[CurrentPlayerId]
                                             : IsOnBoard(fromIndex) ? _nodes[fromIndex].Checkers
                                             : null;
            if (stack == null)
                return null;

            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (CanPlayChecker(stack[i], fromIndex))
                    return stack[i];
            }

            return null;
        }

        /// <summary>
        /// Can the current player play THIS checker right now — move it, bear it off or free it from a lock?
        /// Does NOT touch the board. The View uses it to mark the checkers that can move with the rolled dice.
        ///
        /// Freeing a locked checker (<see cref="CanReleaseChecker(BaseChecker)"/>) counts as playable: it spends a
        /// die, so the turn must not be passed while it is still possible.
        /// </summary>
        public bool CanPlayChecker(BaseChecker checker)
        {
            return checker != null && CanPlayChecker(checker, FindNodeIndexOf(checker));
        }

        private bool CanPlayChecker(BaseChecker checker, int fromIndex)
        {
            if (IsMatchOver || checker.OwnerId != CurrentPlayerId)
                return false;

            // Cheap short circuit: nothing to scan once all dice are spent.
            DiceRoll roll = Context != null ? Context.CurrentRoll : null;
            if (roll != null && !roll.HasMovesLeft)
                return false;

            int player = CurrentPlayerId;

            if (GetBarCount(player) > 0)
                return fromIndex == BarIndex && HasAnyTarget(BarIndex, checker);

            if (!IsOnBoard(fromIndex))
                return false;

            return ResolveBearOff(fromIndex, player, false, checker).Success
                || HasAnyTarget(fromIndex, checker)
                || ResolveRelease(fromIndex, player, false, checker);
        }

        /// <summary>Allocation-free variant of GetLegalTargets that stops at the first hit.</summary>
        private bool HasAnyTarget(int fromIndex, BaseChecker chosen)
        {
            for (int to = 0; to < NodeCount; to++)
            {
                if (to != fromIndex && CanReach(fromIndex, to, chosen))
                    return true;
            }

            return false;
        }

        #endregion

        #region Move resolution

        /// <param name="payment">
        /// How the move is paid for. Only a free mover (<see cref="IFreeMover"/>) may pay with every die left;
        /// TryMove and CanReach try that once the dice have failed.
        /// </param>
        /// <param name="chosen">The checker to move; null for the player's topmost one on the source.</param>
        private MoveResult ResolveMove(int fromIndex, int toIndex, int playerId, bool commit,
                                       MovePayment payment = MovePayment.Die, BaseChecker chosen = null)
        {
            // ---------- 0) Time bomb: once the match is closed no move is accepted ----------
            if (IsMatchOver)
                return MoveResult.Fail(MoveError.MatchOver, fromIndex, toIndex, playerId);

            // A Bowman's volley must be fired before anything else is played (GameState.Volleys.cs).
            if (IsAwaitingVolley)
                return MoveResult.Fail(MoveError.VolleyPending, fromIndex, toIndex, playerId);

            // Bearing off is a separate path: the destination is not a point but the area off the board.
            // It goes through the same ResolveMove gate so both TryMove and CanMove support it.
            if (toIndex == BearOffIndex)
                return ResolveBearOff(fromIndex, playerId, commit, chosen);

            // ---------- 1) Basic validation ----------
            if (!IsOnBoard(toIndex) || (!IsOnBoard(fromIndex) && fromIndex != BarIndex))
                return MoveResult.Fail(MoveError.OutOfBounds, fromIndex, toIndex, playerId);

            if (GetBarCount(playerId) > 0 && fromIndex != BarIndex)
                return MoveResult.Fail(MoveError.MustEnterFromBar, fromIndex, toIndex, playerId);

            BaseChecker mover = PeekSource(fromIndex, playerId, chosen);
            if (mover == null)
                return MoveResult.Fail(MoveError.SourceEmpty, fromIndex, toIndex, playerId);

            if (mover.OwnerId != playerId)
                return MoveResult.Fail(MoveError.NotYourChecker, fromIndex, toIndex, playerId);

            // ---------- 2) Permission to leave (Swamp lock / Dungeon bars) ----------
            if (IsOnBoard(fromIndex))
            {
                ITileEffect sourceTile = TileFor(_nodes[fromIndex], mover);
                if (sourceTile != null && !sourceTile.CanLeaveFrom(mover))
                    return MoveResult.Fail(MoveError.CheckerLocked, fromIndex, toIndex, playerId);
            }

            // ---------- 3) Direction and distance ----------
            int origin = fromIndex == BarIndex ? GetBarOrigin(playerId) : fromIndex;
            int distance = (toIndex - origin) * GetDirection(playerId);

            if (distance <= 0)
            {
                // Voodoo Checker: backwards it may swap places with a friendly checker (GameState.Swaps.cs).
                if (distance < 0 && payment != MovePayment.AllDice && IsOnBoard(fromIndex) &&
                    mover.GetAbility<ISwapMover>() != null)
                    return ResolveSwap(fromIndex, toIndex, playerId, mover, -distance, commit);

                return MoveResult.Fail(MoveError.WrongDirection, fromIndex, toIndex, playerId);
            }

            int requiredDie = distance;

            if (payment == MovePayment.AllDice)
            {
                // Teleporter Checker: any distance, paid below with every die left.
                if (mover.GetAbility<IFreeMover>() == null || !HasDiceLeft())
                    return MoveResult.Fail(MoveError.NoDiceValue, fromIndex, toIndex, playerId);
            }
            else
            {
                // Nomad Checker: extra points when the point a die brings it to has empty neighbours. The die
                // pays only its own part, so the player can move 6 points with a 3 but gains no extra move.
                // Whenever the bonus can pay its part it does, even if a die also covers the whole distance:
                // with 6-3 in hand, moving 6 points spends the 3 and keeps the 6.
                // A planned compound step pays exactly the die it was planned with, or the plan would break.
                int bonusDie = payment == MovePayment.Die ? FindBonusDie(mover, origin, playerId, distance) : 0;
                if (bonusDie > 0)
                    requiredDie = bonusDie;

                if (requiredDie <= 0 || requiredDie > MaxDieValue)
                    return MoveResult.Fail(MoveError.InvalidDistance, fromIndex, toIndex, playerId);

                if (!CanAffordDistance(requiredDie))
                    return MoveResult.Fail(MoveError.NoDiceValue, fromIndex, toIndex, playerId);
            }

            // ---------- 4-6) Destination ----------
            List<BaseChecker> captures;
            bool infiltration;
            MoveError landing = CheckLanding(mover, toIndex, playerId, commit, out captures, out infiltration);
            if (landing != MoveError.None)
                return MoveResult.Fail(landing, fromIndex, toIndex, playerId);

            // ===================================================================
            // From here on every rule has passed and the game state changes.
            // ===================================================================

            // A dry query (CanMove / GetLegalTargets) stops exactly here: it runs the SAME validation
            // code but never touches the board. Because the rules are not written twice, a
            // "highlighted point is legal but the move is rejected" mismatch is impossible.
            if (!commit)
                return MoveResult.Ok(fromIndex, toIndex, distance, playerId, mover, captures, infiltration, 0);

            if (payment == MovePayment.AllDice)
            {
                ConsumeAllDice();
                ReportAbility(AbilityIds.FreeMove, mover, toIndex);
            }
            else
            {
                ConsumeDistance(requiredDie);

                // Reported only now: a move the destination refuses has not used the bonus.
                if (requiredDie < distance)
                    ReportAbility(AbilityIds.ExtraDistance, mover, toIndex);
            }

            // Drunken Checker: once the move is paid for, it may stagger past or short of the chosen point.
            if (SideEffectsEnabled)
                ApplyDeviation(mover, playerId, ref toIndex, ref distance, ref captures, ref infiltration);

            if (IsOnBoard(fromIndex))
            {
                RemoveFromNode(_nodes[fromIndex], mover);
            }
            else
            {
                _bar[playerId].Remove(mover);
            }

            // Notify checkers/tiles along the path (Bandit Checker, Toll Booth).
            NotifyPassOver(origin, toIndex, playerId, mover);

            // Navigator Checker: the footprint goes down as it lands, so the capture below already scores on it.
            LeaveLandingMark(mover, toIndex);

            // Aggressive scoring: every captured enemy checker scores.
            // ApplyCapture returns the awarded amount, we only sum it up. The mover is the one who
            // earned it, so its own score modifier (Catalyst) applies.
            Node target = _nodes[toIndex];
            int scoreGained = 0;
            bool retaliation = false;
            if (captures != null)
            {
                for (int i = 0; i < captures.Count; i++)
                {
                    bool retaliates;
                    scoreGained += ApplyCapture(captures[i], target, playerId, mover, out retaliates);
                    retaliation |= retaliates;
                }
            }

            target.Push(mover);

            MoveResult result = MoveResult.Ok(fromIndex, toIndex, distance, playerId, mover, captures,
                                              infiltration, scoreGained);

            // Tile effects apply AFTER the checker has settled (Spike Pit, Maelstrom, Fog, Swamp...).
            ApplyLandingTile(target, mover);

            // Gunpowder Checker: a victim that blows up its capturer takes the mover down with it — unless the
            // tile above has already moved the mover on. It is a rule, so it applies in simulations too.
            if (retaliation && FindNodeIndexOf(mover) == toIndex)
            {
                ReportAbility(AbilityIds.CaptureRetaliation, mover, toIndex);
                ForceCapture(mover, toIndex, GetOpponent(playerId));
            }

            NotifyMoveReactors(target, mover, result);

            // A captured checker gets its abilities back once its entry is over — unless the entry itself has sent it
            // back to the bar (a Spike Pit, a Gunpowder blast).
            if (fromIndex == BarIndex && SideEffectsEnabled && FindNodeIndexOf(mover) != BarIndex)
                mover.SetReentering(false);

            return result;
        }

        /// <summary>
        /// Steps 4-6 of a move: may this checker settle on the point, and what does landing there capture?
        /// Shared by every move and by the Drunken Checker's stagger, so both obey exactly the same rules.
        /// </summary>
        /// <param name="commit">A real attempt: reports abilities and spends single-use shields.</param>
        private MoveError CheckLanding(BaseChecker mover, int toIndex, int playerId, bool commit,
                                       out List<BaseChecker> captures, out bool infiltration)
        {
            captures = null;
            infiltration = false;

            // ---------- 4) Destination: tile permission ----------
            Node target = _nodes[toIndex];

            ITileEffect targetTile = TileFor(target, mover);
            if (targetTile != null && !targetTile.CanLandOn(mover))
                return MoveError.BlockedByTile;

            // ---------- 5) Destination: enemy checkers ----------
            bool sweep = false;
            if (target.CountEnemiesOf(playerId) > 0)
            {
                IAreaCapture area = mover.GetAbility<IAreaCapture>();
                IInfiltrator infiltrator = mover.GetAbility<IInfiltrator>();

                if (area != null && area.CanCaptureEntirePoint(target))
                {
                    // Doomsday / Hammer Checker: sweeps the whole point.
                    captures = target.GetEnemiesOf(playerId);
                    sweep = true;
                }
                else if (infiltrator != null && infiltrator.CanInfiltrate(target))
                {
                    // Parasite Checker: settles on the point next to the enemy without capturing.
                    infiltration = true;
                }
                else
                {
                    BaseChecker victim = target.GetTopEnemyOf(playerId);
                    bool singleEnemy = target.CountEnemiesOf(playerId) == 1;

                    if (singleEnemy && victim.CanBeCaptured && !target.IsBlockedFor(playerId))
                    {
                        captures = new List<BaseChecker> { victim };
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

                        return MoveError.DestinationBlocked;
                    }
                }
            }

            // ---------- 6) Capacity ----------
            // Every checker that stays on the point counts: an infiltrating Parasite shares the limit with the
            // enemies it moves in with, while captured checkers make room as they leave.
            int staying = target.CheckerCount - (captures != null ? captures.Count : 0);
            if (staying + 1 > StackLimit)
                return MoveError.StackFull;

            // Reported only now: a landing the capacity refuses has used neither ability.
            if (commit && sweep)
                ReportAbility(AbilityIds.AreaCapture, mover, toIndex);
            else if (commit && infiltration)
                ReportAbility(AbilityIds.Infiltrate, mover, toIndex);

            return MoveError.None;
        }

        /// <summary>
        /// Drunken Checker: the move is already paid for; now the checker staggers one point past or short of the
        /// chosen point. It only does so where it could legally land — off the board, back onto its own point or
        /// onto a point it may not enter, it simply lands where it was aimed.
        /// </summary>
        private void ApplyDeviation(BaseChecker mover, int playerId, ref int toIndex, ref int distance,
                                    ref List<BaseChecker> captures, ref bool infiltration)
        {
            IDistanceDeviation deviation = mover.GetAbility<IDistanceDeviation>();
            if (deviation == null)
                return;

            int offset = deviation.RollDeviation(Context.Random);
            int staggered = toIndex + offset * GetDirection(playerId);
            if (offset == 0 || distance + offset <= 0 || !IsOnBoard(staggered))
                return;

            // A dry look first, so a refused point spends no shield; then the real one, which reports abilities.
            List<BaseChecker> dryCaptures;
            bool dryInfiltration;
            if (CheckLanding(mover, staggered, playerId, false, out dryCaptures, out dryInfiltration) != MoveError.None)
                return;

            CheckLanding(mover, staggered, playerId, true, out captures, out infiltration);
            ReportAbility(AbilityIds.Deviation, mover, staggered);

            toIndex = staggered;
            distance += offset;
        }

        /// <summary>
        /// The player's checker that acts from this source: their topmost one or, if one was chosen, that checker —
        /// provided it is theirs and stands there. Null if there is none.
        /// </summary>
        private BaseChecker PeekSource(int fromIndex, int playerId, BaseChecker chosen = null)
        {
            IReadOnlyList<BaseChecker> stack = fromIndex == BarIndex ? _bar[playerId] : _nodes[fromIndex].Checkers;

            // The point may also hold enemy checkers (Parasite), so look for OUR checker.
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                BaseChecker checker = stack[i];
                if (checker.OwnerId == playerId && (chosen == null || ReferenceEquals(checker, chosen)))
                    return checker;
            }
            return null;
        }

        /// <summary>
        /// Nomad Checker: the smallest die in hand that, with the bonus earned on the point it brings the checker
        /// to, covers <paramref name="distance"/> exactly; 0 if there is none or the checker has no bonus.
        /// </summary>
        private int FindBonusDie(BaseChecker mover, int origin, int playerId, int distance)
        {
            IExtraDistanceProvider extra = mover.GetAbility<IExtraDistanceProvider>();
            if (extra == null)
                return 0;

            int direction = GetDirection(playerId);
            for (int die = 1; die < distance && die <= MaxDieValue; die++)
            {
                if (CanAffordDistance(die) &&
                    die + extra.GetBonusDistance(this, origin + die * direction) == distance)
                    return die;
            }

            return 0;
        }

        #endregion
    }
}

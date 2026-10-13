using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Board
{
    // Combining several dice into one multi-step move for a single checker.
    public partial class GameState
    {
        // Scratch buffers for dry planning. CanReach runs up to 24 times per source point for every
        // highlight and every bot evaluation, so allocating new lists there would create constant garbage.
        // Planning is not re-entrant (no events are raised while planning), so sharing them is safe.
        private readonly List<int> _planScratch = new List<int>(4);
        private readonly List<int> _diceScratch = new List<int>(4);

        /// <summary>
        /// Is there a path that reaches this destination by combining dice? Does NOT touch the board.
        /// </summary>
        /// <param name="chosen">The checker to move; null for the topmost one on the source.</param>
        private bool CanPlanCompound(int fromIndex, int toIndex, int playerId, BaseChecker chosen)
        {
            BaseChecker mover;
            return TryBuildPlan(fromIndex, toIndex, playerId, chosen, _planScratch, out mover);
        }

        /// <summary>
        /// Executes moves such as "5 points with 3-2 in hand".
        ///
        /// CRITICAL: this is NOT a single jump. The plan is executed as consecutive REAL moves —
        /// the intermediate stop is really visited. That way a single enemy checker on the
        /// intermediate point is captured, a spike/fog/maelstrom tile there is triggered and
        /// abilities fire. None of this needs extra code.
        /// </summary>
        /// <param name="chosen">The checker to move; null for the topmost one on the source.</param>
        private bool TryCompoundMove(int fromIndex, int toIndex, int playerId, BaseChecker chosen)
        {
            // A dedicated list (not the scratch buffer): executing steps raises events, and a
            // listener may legitimately call CanReach, which reuses the scratch buffer.
            List<int> plan = new List<int>(4);
            BaseChecker mover;
            if (!TryBuildPlan(fromIndex, toIndex, playerId, chosen, plan, out mover))
                return false;

            int direction = GetDirection(playerId);
            int current = fromIndex;
            bool anyStepDone = false;

            for (int i = 0; i < plan.Count; i++)
            {
                int next = current + plan[i] * direction;

                // Every step pays exactly the die it was planned with — a Nomad's bonus would spend a smaller
                // die the plan still needs — and moves the same checker, wherever it sits in its new stack.
                // A volley the first step arms (Bowman) waits for the whole move, so it does not stop the later steps;
                // the listeners of each step, run below, already see it waiting.
                MoveResult step;
                _continuingPlan = anyStepDone;
                try
                {
                    step = ResolveMove(current, next, playerId, true, MovePayment.ExactDie, mover);
                }
                finally
                {
                    _continuingPlan = false;
                }

                if (!step.Success)
                {
                    // A tile or ability on an intermediate stop broke the plan. The checker stays where it is;
                    // if no step was taken, the calling layer reports the rejection.
                    return anyStepDone;
                }

                anyStepDone = true;
                MoveExecuted?.Invoke(step);

                // The checker may have been teleported (Maelstrom), captured (Spike Pit) or borne off.
                // Continue from its REAL position, not from the planned one.
                current = FindNodeIndexOf(mover);

                if (current != next)
                    break; // Plan broken; remaining steps are cancelled.
            }

            return anyStepDone;
        }

        /// <summary>
        /// Searches for a sequence of dice that reaches the destination (depth at most 4 — a double).
        /// The dice multiset is managed by hand: a value cannot be used more often than it is held.
        /// </summary>
        /// <param name="mover">The checker the plan moves; null when there is no plan.</param>
        private bool TryBuildPlan(int fromIndex, int toIndex, int playerId, BaseChecker chosen, List<int> plan,
                                  out BaseChecker mover)
        {
            plan.Clear();
            mover = null;

            if (!IsOnBoard(fromIndex) || !IsOnBoard(toIndex) || fromIndex == toIndex)
                return false;

            if (IsMatchOver || IsAwaitingVolley || GetBarCount(playerId) > 0)
                return false;

            DiceRoll roll = Context != null ? Context.CurrentRoll : null;

            // No dice system, or Cartographer's Cheat already allows free splitting —
            // no need to combine, those paths are handled as a single move.
            if (roll == null || roll.FreeSplit)
                return false;

            BaseChecker checker = PeekSource(fromIndex, playerId, chosen);
            if (checker == null)
                return false;

            // A lock on the source point (Swamp / Dungeon) blocks compound moves as well.
            Node source = _nodes[fromIndex];
            ITileEffect sourceTile = TileFor(source, checker);
            if (sourceTile != null && !sourceTile.CanLeaveFrom(checker))
                return false;

            List<int> available = _diceScratch;
            available.Clear();
            IReadOnlyList<int> remaining = roll.RemainingMoves;
            for (int i = 0; i < remaining.Count; i++)
                available.Add(remaining[i]);

            // This should not be reached when a single die already suffices; we still look for
            // paths that need at least two dice.
            if (!SearchPlan(checker, fromIndex, toIndex, playerId, available, plan))
                return false;

            mover = checker;
            return true;
        }

        private bool SearchPlan(BaseChecker mover, int currentIndex, int targetIndex, int playerId,
                                List<int> available, List<int> plan)
        {
            int direction = GetDirection(playerId);

            for (int i = 0; i < available.Count; i++)
            {
                int die = available[i];

                // Do not try the same value twice on this level (avoids redundant branches on doubles).
                bool duplicate = false;
                for (int j = 0; j < i; j++)
                {
                    if (available[j] == die)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                    continue;

                int next = currentIndex + die * direction;

                if (!IsOnBoard(next))
                    continue;

                // Overshooting the target kills this branch.
                if ((next - targetIndex) * direction > 0)
                    continue;

                if (!CanOccupy(mover, next, playerId))
                    continue;

                available.RemoveAt(i);
                plan.Add(die);

                if (next == targetIndex)
                    return true;

                if (SearchPlan(mover, next, targetIndex, playerId, available, plan))
                    return true;

                plan.RemoveAt(plan.Count - 1);
                available.Insert(i, die);
            }

            return false;
        }

        /// <summary>
        /// "Can this checker be placed on this point?" — an ESTIMATE for intermediate stops.
        /// ResolveMove cannot be used because the checker is not there yet (PeekSource would return nothing).
        /// The final decision is still made by ResolveMove; this only guides planning.
        /// If the two disagree, the compound move stops early and the board stays consistent.
        /// </summary>
        private bool CanOccupy(BaseChecker mover, int toIndex, int playerId)
        {
            Node target = _nodes[toIndex];

            ITileEffect targetTile = TileFor(target, mover);
            if (targetTile != null && !targetTile.CanLandOn(mover))
                return false;

            // The checkers left on the point once the mover lands, as in ResolveMove's capacity check.
            int staying = target.CountFor(playerId);
            int enemies = target.CountEnemiesOf(playerId);

            if (enemies > 0)
            {
                IAreaCapture area = mover.GetAbility<IAreaCapture>();
                IInfiltrator infiltrator = mover.GetAbility<IInfiltrator>();

                // Swept or captured enemies make room; only an infiltrating Parasite leaves them standing.
                bool sweeps = area != null && area.CanCaptureEntirePoint(target);
                if (!sweeps)
                {
                    if (infiltrator != null && infiltrator.CanInfiltrate(target))
                        staying += enemies;
                    else if (target.IsBlockedFor(playerId))
                        return false;
                }
            }

            return staying + 1 <= StackLimit;
        }
    }
}

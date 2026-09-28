using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;
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
        private bool CanPlanCompound(int fromIndex, int toIndex, int playerId)
        {
            return TryBuildPlan(fromIndex, toIndex, playerId, _planScratch);
        }

        /// <summary>
        /// Executes moves such as "5 points with 3-2 in hand".
        ///
        /// CRITICAL: this is NOT a single jump. The plan is executed as consecutive REAL moves —
        /// the intermediate stop is really visited. That way a single enemy checker on the
        /// intermediate point is captured, a spike/fog/maelstrom tile there is triggered and
        /// abilities fire. None of this needs extra code.
        /// </summary>
        private bool TryCompoundMove(int fromIndex, int toIndex, int playerId)
        {
            // A dedicated list (not the scratch buffer): executing steps raises events, and a
            // listener may legitimately call CanReach, which reuses the scratch buffer.
            List<int> plan = new List<int>(4);
            if (!TryBuildPlan(fromIndex, toIndex, playerId, plan))
                return false;

            int direction = GetDirection(playerId);
            int current = fromIndex;
            bool anyStepDone = false;

            for (int i = 0; i < plan.Count; i++)
            {
                int next = current + plan[i] * direction;

                MoveResult step = ResolveMove(current, next, playerId, true);

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
                BaseChecker mover = step.MovedChecker;
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
        private bool TryBuildPlan(int fromIndex, int toIndex, int playerId, List<int> plan)
        {
            plan.Clear();

            if (!IsOnBoard(fromIndex) || !IsOnBoard(toIndex) || fromIndex == toIndex)
                return false;

            if (IsMatchOver || GetBarCount(playerId) > 0)
                return false;

            DiceRoll roll = Context != null ? Context.CurrentRoll : null;

            // No dice system, or Cartographer's Cheat already allows free splitting —
            // no need to combine, those paths are handled as a single move.
            if (roll == null || roll.FreeSplit)
                return false;

            BaseChecker mover = PeekSource(fromIndex, playerId);
            if (mover == null || mover.OwnerId != playerId)
                return false;

            // A lock on the source point (Swamp / Dungeon) blocks compound moves as well.
            Node source = _nodes[fromIndex];
            if (source.ActiveTile != null && !source.ActiveTile.CanLeaveFrom(mover))
                return false;

            List<int> available = _diceScratch;
            available.Clear();
            IReadOnlyList<int> remaining = roll.RemainingMoves;
            for (int i = 0; i < remaining.Count; i++)
                available.Add(remaining[i]);

            // This should not be reached when a single die already suffices; we still look for
            // paths that need at least two dice.
            return SearchPlan(mover, fromIndex, toIndex, playerId, available, plan);
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

            if (target.ActiveTile != null && !target.ActiveTile.CanLandOn(mover))
                return false;

            if (target.CountEnemiesOf(playerId) > 0)
            {
                IAreaCapture area = mover.GetAbility<IAreaCapture>();
                IInfiltrator infiltrator = mover.GetAbility<IInfiltrator>();
                IBlockadeBypass bypass = mover.GetAbility<IBlockadeBypass>();

                bool allowed =
                    (area != null && area.CanCaptureEntirePoint(target)) ||
                    (infiltrator != null && infiltrator.CanInfiltrate(target)) ||
                    (bypass != null && bypass.CanBypassBlockade(target)) ||
                    !target.IsBlockedFor(playerId);

                if (!allowed)
                    return false;
            }

            return target.CountFor(playerId) + 1 <= target.GetStackLimit(DefaultStackLimit);
        }
    }
}

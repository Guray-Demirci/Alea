using System;
using System.Collections.Generic;
using Tavla.Core.Abilities;
using Tavla.Core.Checkers;

namespace Tavla.Core.Board
{
    // A volley waiting for its owner to pick the checkers it hits (Bowman).
    public partial class GameState
    {
        // The checker whose volley waits for its targets, and its arrows; null when none waits. Armed by the shooter's
        // move, cleared once the volley is fired or the turn ends.
        private BaseChecker _volleyShooter;
        private int _volleyArrows;

        // Set while a compound move plays one of its planned steps: a volley armed on the first step waits until the
        // whole move is over, so it must not stop the steps after it.
        private bool _continuingPlan;

        /// <summary>
        /// A volley's arrows have landed: each target was captured, one after another, unless the match ended part
        /// way. (shooter, targets in the order they were hit)
        /// </summary>
        public event Action<BaseChecker, IReadOnlyList<BaseChecker>> VolleyFired;

        /// <summary>
        /// Is a volley waiting for its targets? Until the current player picks them (<see cref="TryFireVolley"/>)
        /// nothing else can be played — no move, bear-off, release or sacrifice — and the turn is not over
        /// (<see cref="HasAnyLegalMove"/>). It stops waiting once fired, when the turn or the match ends, or when no
        /// checker is left for it to hit.
        /// </summary>
        public bool HasPendingVolley => _volleyShooter != null && !IsMatchOver && HasVolleyTarget();

        /// <summary>The checker whose volley waits for its targets; null when none waits.</summary>
        public BaseChecker PendingVolleyShooter => HasPendingVolley ? _volleyShooter : null;

        /// <summary>
        /// How many targets the waiting volley must be given: one per arrow, or every checker it can hit if there are
        /// fewer. 0 when no volley waits.
        /// </summary>
        public int PendingVolleySize => HasPendingVolley ? Math.Min(_volleyArrows, CountVolleyTargets()) : 0;

        /// <summary>
        /// Can the waiting volley hit this checker? Any of the opponent's checkers on the board can be hit, wherever it
        /// stands — on a made point too — but not one that cannot be captured (Armored), nor one already on the bar.
        /// </summary>
        public bool CanVolleyTarget(BaseChecker checker)
        {
            return checker != null && HasPendingVolley && IsVolleyTarget(checker);
        }

        /// <summary>
        /// Clears <paramref name="results"/> and fills it with every checker the waiting volley can hit, point by point,
        /// bottom of each stack first. Left empty when no volley waits.
        /// </summary>
        public void GetVolleyTargets(List<BaseChecker> results)
        {
            results.Clear();
            if (!HasPendingVolley)
                return;

            for (int i = 0; i < NodeCount; i++)
            {
                Node node = _nodes[i];
                for (int c = 0; c < node.CheckerCount; c++)
                {
                    if (IsHittable(node.Checkers[c]))
                        results.Add(node.Checkers[c]);
                }
            }
        }

        /// <summary>
        /// Fires the waiting volley at the chosen checkers: exactly <see cref="PendingVolleySize"/> different ones the
        /// volley can hit (<see cref="CanVolleyTarget"/>). Each is captured as if by a tile or the Clover: nothing
        /// lands on it, so it scores its capture for the shooter's owner but a Gunpowder Checker has no one to take
        /// down. A rejected choice changes nothing and the volley keeps waiting.
        /// </summary>
        public bool TryFireVolley(IReadOnlyList<BaseChecker> targets)
        {
            if (targets == null || !HasPendingVolley || targets.Count != PendingVolleySize)
                return false;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null || !IsVolleyTarget(targets[i]))
                    return false;

                for (int j = 0; j < i; j++)
                {
                    if (ReferenceEquals(targets[i], targets[j]))
                        return false;
                }
            }

            BaseChecker shooter = _volleyShooter;
            BaseChecker[] hit = new BaseChecker[targets.Count];
            for (int i = 0; i < hit.Length; i++)
                hit[i] = targets[i];

            DropVolley();

            // A capture may reach the target score; the arrows still in the air are lost with the match.
            for (int i = 0; i < hit.Length && !IsMatchOver; i++)
            {
                int point = FindNodeIndexOf(hit[i]);
                ReportAbility(AbilityIds.BowmanArrow, shooter, point);
                ForceCapture(hit[i], point, shooter.OwnerId);
            }

            VolleyFired?.Invoke(shooter, hit);
            return true;
        }

        /// <summary>
        /// The Bowman's move has earned a volley of <paramref name="arrows"/>; it now waits for its targets. Returns
        /// false — and nothing waits — when there is nothing to hit. Never in a simulation: the bot's look-ahead must
        /// not leave a volley on its copy of the board.
        /// </summary>
        internal bool ArmVolley(BaseChecker shooter, int arrows)
        {
            if (shooter == null || arrows <= 0 || IsMatchOver || !SideEffectsEnabled)
                return false;

            _volleyShooter = shooter;
            _volleyArrows = arrows;

            if (HasVolleyTarget())
                return true;

            DropVolley();
            return false;
        }

        /// <summary>
        /// Must the current player fire the waiting volley before anything else? Not while a compound move plays the
        /// steps after the one that armed it.
        /// </summary>
        private bool IsAwaitingVolley => !_continuingPlan && HasPendingVolley;

        private void DropVolley()
        {
            _volleyShooter = null;
            _volleyArrows = 0;
        }

        private bool IsVolleyTarget(BaseChecker checker)
        {
            return IsHittable(checker) && IsOnBoard(FindNodeIndexOf(checker));
        }

        /// <summary>Could an arrow of the waiting volley capture this checker, were it on the board?</summary>
        private bool IsHittable(BaseChecker checker)
        {
            return checker.IsEnemyOf(_volleyShooter.OwnerId) && checker.CanBeCaptured;
        }

        private bool HasVolleyTarget() => CountVolleyTargets() > 0;

        private int CountVolleyTargets()
        {
            int count = 0;
            for (int i = 0; i < NodeCount; i++)
            {
                Node node = _nodes[i];
                for (int c = 0; c < node.CheckerCount; c++)
                {
                    if (IsHittable(node.Checkers[c]))
                        count++;
                }
            }

            return count;
        }
    }
}

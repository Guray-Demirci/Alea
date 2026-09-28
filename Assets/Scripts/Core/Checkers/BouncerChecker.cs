using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Bouncer Checker. "50% chance to hit the first enemy checker ahead of it directly,
    /// 25% chance to hit itself."
    /// A single roll (0-99) separates all three outcomes: 0-49 hits, 50-74 self-hit, 75-99 nothing.
    /// </summary>
    public class BouncerChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_bouncer";

        private const int CaptureThreshold = 50;   // 50%
        private const int SelfHitThreshold = 75;   // the next 25%

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover)
                return;

            int roll = context.Random.Range(0, 100);

            if (roll < CaptureThreshold)
            {
                CaptureFirstEnemyAhead(context, result.ToIndex);
            }
            else if (roll < SelfHitThreshold)
            {
                // Hits itself. It goes through ForceCapture, so the Phoenix/Sacrificial rules also apply.
                context.Board.ReportAbility(AbilityIds.BouncerSelfHit, this, result.ToIndex);
                context.Board.ForceCapture(this, result.ToIndex, OwnerId);
            }
        }

        /// <summary>Finds and captures the first enemy checker in the direction of travel.</summary>
        private void CaptureFirstEnemyAhead(IMatchContext context, int fromIndex)
        {
            int step = GameState.GetDirection(OwnerId);

            for (int index = fromIndex + step; GameState.IsOnBoard(index); index += step)
            {
                Node node = context.Board.Nodes[index];
                BaseChecker enemy = node.GetTopEnemyOf(OwnerId);

                if (enemy == null)
                    continue;

                // Checkers with immunity (e.g. Armored) are not affected by this ability either.
                if (!enemy.CanBeCaptured)
                    return;

                context.Board.ReportAbility(AbilityIds.BouncerStrike, this, index);
                context.Board.ForceCapture(enemy, index, OwnerId);
                return;
            }
        }

        public string ItemId => Id;
        public string DisplayName => "Bouncer Checker";
        public int Price => 600;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public BouncerChecker(int ownerId) : base(ownerId) { }
    }
}

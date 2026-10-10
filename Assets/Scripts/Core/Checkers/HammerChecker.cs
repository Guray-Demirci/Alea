using Tavla.Core.Abilities;
using Tavla.Core.Board;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Hammer Checker. "It can capture two enemy checkers at once. If the target point holds 1-2 enemy checkers it
    /// captures them, but it cannot be played onto a point holding 3."
    /// The Doomsday Checker's ability with a cap: it smashes small points only, and every checker on them.
    /// </summary>
    public class HammerChecker : SpecialChecker, IAreaCapture
    {
        public const string Id = "checker_hammer";
        private const int MaxVictims = 2;

        /// <summary>Every enemy on the point must be capturable — an Armored Checker stops the hammer.</summary>
        public bool CanCaptureEntirePoint(Node target)
        {
            int enemies = target.CountEnemiesOf(OwnerId);
            if (enemies < 1 || enemies > MaxVictims)
                return false;

            for (int i = 0; i < target.CheckerCount; i++)
            {
                BaseChecker checker = target.Checkers[i];
                if (checker.IsEnemyOf(OwnerId) && !checker.CanBeCaptured)
                    return false;
            }

            return true;
        }

        public HammerChecker(int ownerId) : base(ownerId, Id, "Hammer Checker") { }
    }
}

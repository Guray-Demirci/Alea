using Tavla.Core.Abilities;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Second Chance Checker. "It is no use while it stands on the board. But at any moment (even mid-roll) you
    /// can remove it from the board, sacrificing it for good, to gain an extra move. A lifesaver in emergencies."
    /// The extra move is one more die, rolled when it is sacrificed. "For good" means the run too: it also leaves
    /// the player's deck once the match ends.
    /// </summary>
    public class SecondChanceChecker : SpecialChecker, ISacrificeAbility
    {
        public const string Id = "checker_secondchance";

        public void OnSacrificed(IMatchContext context, int nodeIndex)
        {
            context.GrantExtraDie(OwnerId);
            context.Board.ReportAbility(AbilityIds.SecondChanceBonus, this, nodeIndex);
        }

        public SecondChanceChecker(int ownerId) : base(ownerId, Id, "Second Chance Checker") { }
    }
}

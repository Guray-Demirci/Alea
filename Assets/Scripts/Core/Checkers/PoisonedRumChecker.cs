using Tavla.Core.Abilities;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Poisoned Rum Checker. "If an opponent captures it (sends it to the bar), the opponent is poisoned and can
    /// roll only one die on their next turn (the second die counts as passed)."
    /// </summary>
    public class PoisonedRumChecker : SpecialChecker, ICaptureReactor
    {
        public const string Id = "checker_poisonedrum";

        public void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex)
        {
            // Only the opponent drinks it: its own side's hit (a Clover hitting itself) poisons nobody.
            if (capturedByPlayerId == OwnerId)
                return;

            context.RestrictNextRollToOneDie(capturedByPlayerId);
            context.Board.ReportAbility(AbilityIds.RumPoisoned, this, nodeIndex);
        }

        public PoisonedRumChecker(int ownerId) : base(ownerId, Id, "Poisoned Rum Checker") { }
    }
}

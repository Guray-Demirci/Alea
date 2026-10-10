using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Meridian Shift (die-pinning card) — SINGLE USE.
    /// "When activated at the start of a turn, one of the dice you roll this turn is pinned to the number you choose."
    /// The charge counter and armed state live in <see cref="ConsumableRelic"/>; only the effect is here.
    /// </summary>
    public class MeridianShiftRelic : ConsumableRelic
    {
        public override string ItemId => "relic_meridian_shift";
        public override string DisplayName => "Meridian Shift";

        /// <summary>It overrides the result directly, so it must run LAST.</summary>
        public override int Priority => 900;

        /// <summary>The player chooses which number to pin.</summary>
        public override bool RequiresChoice => true;

        private int _pinnedValue = GameState.MaxDieValue;

        public MeridianShiftRelic(int maxCharges = 1) : base(maxCharges) { }

        protected override void OnActivated(IMatchContext context, int choice)
        {
            // An invalid choice falls back to the strongest value.
            _pinnedValue = (choice >= 1 && choice <= GameState.MaxDieValue)
                ? choice
                : GameState.MaxDieValue;
        }

        public override void ModifyRoll(DiceRoll roll, IRandomProvider random)
        {
            if (!IsArmed)
                return;

            // As in the design mock-up: "the left die rolls normally, the right die settles" -> second die.
            roll.OverrideDie(false, _pinnedValue);
            Disarm();
        }
    }
}

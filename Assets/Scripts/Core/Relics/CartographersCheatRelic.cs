using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Cartographer's Cheat — SINGLE USE.
    /// "Lets you split the sum of your dice into any parts and distribute them across any checkers."
    /// Its effect is a single line: it switches the DiceRoll into FreeSplit mode.
    /// </summary>
    public class CartographersCheatRelic : ConsumableRelic
    {
        public override string ItemId => "relic_cartographers_cheat";
        public override string DisplayName => "Cartographer's Cheat";

        /// <summary>Must enable the mode AFTER other relics have decided the result.</summary>
        public override int Priority => 950;

        public CartographersCheatRelic(int maxCharges = 1) : base(maxCharges) { }

        public override void ModifyRoll(DiceRoll roll, IRandomProvider random)
        {
            if (!IsArmed)
                return;

            roll.FreeSplit = true;
            Disarm();
        }
    }
}

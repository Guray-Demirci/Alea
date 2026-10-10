using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Twin Winds — PERMANENT.
    /// "If your two dice differ by exactly 1, the game counts them as a double with a 50% chance."
    /// </summary>
    public class TwinWindsRelic : PermanentRelic
    {
        public override string ItemId => "relic_twin_winds";
        public override string DisplayName => "Twin Winds";

        public override int Priority => 500;

        /// <summary>Balance knob, tuned in one place.</summary>
        private const float UpgradeChance = 0.5f;

        public override void ModifyRoll(DiceRoll roll, IRandomProvider random)
        {
            int difference = roll.First - roll.Second;
            if (difference < 0)
                difference = -difference;

            if (difference != 1)
                return;

            if (!random.Chance(UpgradeChance))
                return;

            // Use the higher die: 5-4 -> 5-5 (in the player's favour).
            int higher = roll.First > roll.Second ? roll.First : roll.Second;
            roll.Set(higher, higher);
        }
    }
}

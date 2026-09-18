using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// A2 - Ikiz Ruzgarlar — KALICI.
    /// "Attigin iki zarin farki sadece 1 ise, oyun bunu %50 ihtimalle cift sayar."
    /// </summary>
    public class TwinWindsRelic : PermanentRelic
    {
        public override string ItemId => "relic_twin_winds";
        public override string DisplayName => "İkiz Rüzgarlar";
        public override int Price => 400;

        public override int Priority => 500;

        /// <summary>Denge ayari tek yerden degistirilir.</summary>
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

            // Buyuk olani baz aliyoruz: 5-4 -> 5-5 (oyuncu lehine).
            int higher = roll.First > roll.Second ? roll.First : roll.Second;
            roll.Set(higher, higher);
        }
    }
}

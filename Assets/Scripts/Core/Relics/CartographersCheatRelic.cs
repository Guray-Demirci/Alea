using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// A4 - Kartografin Hilesi — TEK SEFERLIK.
    /// "Zarlarin toplamini istedigin kadar pula istedigin parcalar halinde dagitma hakki verir."
    /// Etkisi tek satir: DiceRoll'u FreeSplit moduna alir.
    /// </summary>
    public class CartographersCheatRelic : ConsumableRelic
    {
        public override string ItemId => "relic_cartographers_cheat";
        public override string DisplayName => "Kartografın Hilesi";
        public override int Price => 220;

        /// <summary>Diger tilsimlar sonucu belirledikten SONRA modu acmali.</summary>
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

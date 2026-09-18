using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// A1 - Meridyen Kaymasi (Zar Sabitleme Karti) — TEK SEFERLIK.
    /// "Tur basinda aktive ettiginde, o el atacagin zarlardan birini kesin olarak
    /// sectigin sayiya sabitler."
    /// Hak sayaci ve armed durumu ConsumableRelic'te; burada sadece etki var.
    /// </summary>
    public class MeridianShiftRelic : ConsumableRelic
    {
        public override string ItemId => "relic_meridian_shift";
        public override string DisplayName => "Meridyen Kayması";
        public override int Price => 180;

        /// <summary>Sonucu dogrudan ezdigi icin en SON calismali.</summary>
        public override int Priority => 900;

        /// <summary>Oyuncu hangi sayiya sabitleyecegini secer.</summary>
        public override bool RequiresChoice => true;

        private int _pinnedValue = GameState.MaxDieValue;

        public MeridianShiftRelic(int maxCharges = 1) : base(maxCharges) { }

        protected override void OnActivated(IMatchContext context, int choice)
        {
            // Gecersiz secim gelirse en guclu degere duseriz.
            _pinnedValue = (choice >= 1 && choice <= GameState.MaxDieValue)
                ? choice
                : GameState.MaxDieValue;
        }

        public override void ModifyRoll(DiceRoll roll, IRandomProvider random)
        {
            if (!IsArmed)
                return;

            // Dokumandaki gorsel: "sol zar normal doner, sag zar oturur" -> ikinci zar.
            roll.OverrideDie(false, _pinnedValue);
            Disarm();
        }
    }
}

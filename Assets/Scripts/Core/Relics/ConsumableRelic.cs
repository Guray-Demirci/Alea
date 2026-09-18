using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// TEK SEFERLIK tilsimlarin ortak iskeleti. Hak sayaci ve "kullanima alindi mi"
    /// durumu burada TEK kez yazili — her yeni tek kullanimlik tilsim bunu miras alir,
    /// kendi _armed bool'unu tekrar yazmaz.
    ///
    /// Yasam dongusu:
    ///   TryActivate() -> hak duser, IsArmed true olur
    ///   ModifyRoll()  -> etki uygulanir, alt sinif Disarm() cagirir
    ///   IsDepleted    -> hak bitti, envanterden dusulur
    /// </summary>
    public abstract class ConsumableRelic : BaseRelic, IConsumable
    {
        private readonly ChargePool _charges;

        /// <summary>Bu el icin kullanima alindi mi? Alt siniflar etkiyi buna bakarak uygular.</summary>
        protected bool IsArmed { get; private set; }

        protected ConsumableRelic(int maxCharges = 1)
        {
            _charges = new ChargePool(maxCharges);
        }

        // --- IConsumable ---
        public int MaxCharges => _charges.Max;
        public int RemainingCharges => _charges.Remaining;
        public bool IsDepleted => _charges.IsEmpty;

        /// <summary>Varsayilan: secim istemez. Meridyen Kaymasi bunu ezer.</summary>
        public virtual bool RequiresChoice => false;

        public bool TryActivate(IMatchContext context, int choice)
        {
            if (!_charges.TryConsume())
                return false;

            IsArmed = true;
            OnActivated(context, choice);
            return true;
        }

        public void Refill()
        {
            _charges.Refill();
        }

        // --- IDiceModifier ---
        /// <summary>Zar sistemi bu tilsimi SADECE kullanima alindigi el gorur.</summary>
        public override bool IsActive => IsArmed;

        /// <summary>Kullanima alindigi an calisir. Secim gerektiren tilsimlar degeri burada saklar.</summary>
        protected virtual void OnActivated(IMatchContext context, int choice) { }

        /// <summary>Etki uygulandiktan sonra alt sinif bunu cagirir; tilsim susar.</summary>
        protected void Disarm()
        {
            IsArmed = false;
        }
    }
}

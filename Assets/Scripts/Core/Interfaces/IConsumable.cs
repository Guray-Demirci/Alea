using Tavla.Core.Runtime;

namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// SATIN ALINABILIR — TEK SEFERLIK DAL.
    /// Sinirli kullanim hakki olan, harcandiginda biten ogeler.
    /// Meridyen Kaymasi ve Kartografin Hilesi bu dalda; ileride eklenecek
    /// tek kullanimlik kartlar/tuzaklar da buraya girecek.
    ///
    /// Hak sayisi bilerek 1'e sabitlenmedi: "3 kullanimlik" bir kart eklemek
    /// yeni bir arayuz degil, sadece farkli bir MaxCharges degeri gerektirir.
    /// </summary>
    public interface IConsumable : IPurchasable
    {
        /// <summary>Tam doluyken kac kullanim hakki var.</summary>
        int MaxCharges { get; }

        /// <summary>Kalan kullanim hakki.</summary>
        int RemainingCharges { get; }

        /// <summary>Hakki bitti mi? Bittiginde envanterden dusulur.</summary>
        bool IsDepleted { get; }

        /// <summary>
        /// Kullanmadan once oyuncunun bir deger secmesi gerekiyor mu?
        /// (Meridyen Kaymasi: "zar hangi sayiya sabitlensin?") UI buna bakip secim ekrani acar.
        /// </summary>
        bool RequiresChoice { get; }

        /// <summary>
        /// Bir hak harcayarak ogeyi devreye alir. Hak kalmadiysa false doner ve hicbir sey degismez.
        /// </summary>
        /// <param name="choice">RequiresChoice false ise yok sayilir.</param>
        bool TryActivate(IMatchContext context, int choice);

        /// <summary>Dukkandan yeniden dolum.</summary>
        void Refill();
    }
}

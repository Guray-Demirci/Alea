using Tavla.Core.Board;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Yeteneklerin (ability) oyuna dokunabildigi TEK kapi.
    /// Dokumandaki pullar skor, altin, zar ve sira gibi cok farkli sistemlere dokunuyor.
    /// Her yetenege ayri ayri servis enjekte etmek yerine tek bir sozlesme veriyoruz:
    ///  - Yetenek imzalari tek tip kalir (OnMoveCompleted(IMatchContext, ...)),
    ///  - Testte sahte (fake) bir MatchContext yazmak yeterlidir,
    ///  - Yeni bir sistem geldiginde sadece buraya metot eklenir, 30 sinif degismez.
    /// </summary>
    public interface IMatchContext
    {
        GameState Board { get; }
        IRandomProvider Random { get; }
        DiceRoll CurrentRoll { get; }

        // --- Skor ---
        /// <summary>Kasif Pulu gibi duz puan uretenler.</summary>
        /// <returns>Carpanlar uygulandiktan SONRA gercekten yazilan puan.</returns>
        int AddScore(int playerId, int amount);

        /// <summary>Oyuncunun toplam skoru. GameState mac sonunda kazanani bununla belirler.</summary>
        int GetScore(int playerId);

        /// <summary>Katalizor (o el x2) ve Kusatma (kalici x2) carpan biriktirir.</summary>
        void AddScoreMultiplier(int playerId, float multiplier, bool permanent);

        /// <summary>Korsan Koyu: o turda uretilen tum skoru sifirlar.</summary>
        void ResetTurnScore(int playerId);

        // --- Ekonomi ---
        void AddGold(int playerId, int amount);

        /// <summary>Fedakar Pul: dukkandaki tum esyalari kalici olarak ucuzlatir.</summary>
        void AddShopDiscount(int amount);

        /// <summary>Anka Pulu: sonraki dukkan asamasinda sadece nadir+ urunler, %50 indirimli.</summary>
        void TriggerLuxuryShop(int playerId);

        // --- Tur / Zar ---
        /// <summary>Zaman Buken Pul: hedef oyuncunun sirasini atlatir.</summary>
        void SkipTurn(int playerId);

        /// <summary>Sabitleyici Pul & Meridyen Kaymasi: bir sonraki atista bir zari sabitler.</summary>
        void PinNextDie(int playerId, int value);
    }
}

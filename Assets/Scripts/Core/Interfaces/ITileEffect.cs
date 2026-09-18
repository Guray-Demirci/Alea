using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// Bir hanenin (Node) uzerine kurulan ozel karo / tuzak sozlesmesi.
    /// GameState somut karo siniflarini TANIMAZ, sadece bu arayuz uzerinden konusur (DIP).
    ///
    /// Hook seti dokumandaki 8 karonun ihtiyacina gore belirlendi:
    ///   CanLandOn            -> Zindan (kapaliyken giris yok)
    ///   CanLeaveFrom         -> Bataklik (saplanan pul cikamaz), Zindan (kilitli tur)
    ///   BlocksBlockade       -> Zindan (kapaliyken kapi alinamaz)
    ///   OnCheckerLanded      -> Kazik, Girdap, Bataklik, Sis, Haydut Kampi, Barut Ficisi
    ///   OnCheckerLeft        -> Sis (yetenekleri geri ver), Barut Ficisi (sayaci durdur)
    ///   OnCheckerPassedOver  -> Haydut Kampi ("gecen veya oturan her pul")
    ///   OnTurnPassed         -> Zindan (ac-kapa), Barut Ficisi (geri sayim), Korsan Koyu
    /// </summary>
    public interface ITileEffect
    {
        /// <summary>Karo tipinin kimligi. View buna bakip dogru gorseli/VFX'i basar.</summary>
        string EffectId { get; }

        /// <summary>Karoyu kuran oyuncu; harita karosuysa -1 (tarafsiz).</summary>
        int OwnerId { get; }

        /// <summary>Kac tur daha aktif kalacak. Kalici karolar icin -1.</summary>
        int RemainingTurns { get; }

        /// <summary>Suresi doldu mu? Dolduysa GameState karoyu haneden temizler.</summary>
        bool IsExpired { get; }

        /// <summary>Bu pul bu haneye konabilir mi? DOGRULAMA asamasinda cagrilir — yan etkisi olmamali.</summary>
        bool CanLandOn(BaseChecker checker);

        /// <summary>Bu pul bu haneden cikabilir mi? (Bataklik kilidi, Zindan parmakliklari.)</summary>
        bool CanLeaveFrom(BaseChecker checker);

        /// <summary>Bu hanede kapi (blokaj) kurulabilir mi? Zindan kapaliyken false doner.</summary>
        bool AllowsBlockade { get; }

        /// <summary>Pul haneye yerlestikten SONRA. Karonun asil etkisi burada uygulanir.</summary>
        void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker);

        /// <summary>Pul haneden ayrildiktan SONRA. Gecici etkilerin geri alindigi yer.</summary>
        void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker);

        /// <summary>Pul bu hanenin UZERINDEN gectiginde (durmadan). Haydut Kampi bunu kullanir.</summary>
        void OnCheckerPassedOver(IMatchContext context, Node node, BaseChecker checker);

        /// <summary>Her tur sonunda cagrilir. Sayac azaltma / ac-kapa dongusu burada isler.</summary>
        void OnTurnPassed(IMatchContext context, Node node);
    }
}

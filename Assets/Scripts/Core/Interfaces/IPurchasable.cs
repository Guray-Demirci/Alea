namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// Magazadan satin alinabilen her sey (ozel tas, tuzak, board skini) bu arayuzu uygular.
    /// DIKKAT: Burada "satin alindi mi?" bilgisi TUTULMAZ. O bilgi oyuncuya ait bir envanter/
    /// repository katmaninin sorumlulugudur (SRP). Bu arayuz sadece "vitrin bilgisini" tanimlar.
    /// </summary>
    public interface IPurchasable
    {
        /// <summary>Kalici, benzersiz kimlik. Kayit/analitik/magaza eslestirmesi bunun uzerinden yapilir.</summary>
        string ItemId { get; }

        /// <summary>UI'da gosterilecek isim. (Lokalizasyon anahtari da olabilir.)</summary>
        string DisplayName { get; }

        /// <summary>Urunun fiyati. Para birimi tipi ileride ayri bir enum ile genisletilebilir.</summary>
        int Price { get; }
    }
}

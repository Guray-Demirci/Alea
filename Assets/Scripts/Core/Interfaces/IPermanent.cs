namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// SATIN ALINABILIR — KALICI DAL.
    /// Bir kez alinir, mac boyunca (veya kalici olarak) elde kalir: ozel pullar,
    /// kalici zar tilsimlari, board skinleri.
    ///
    /// IConsumable ile birbirini dislar. Bir oge ikisinden BIRINI uygulamak zorundadir;
    /// envanter sistemi sadece IPurchasable uygulayan bir tip gorurse bunu hata sayabilir.
    /// </summary>
    public interface IPermanent : IPurchasable
    {
        /// <summary>
        /// Ayni ogeden birden fazlasina sahip olunabilir mi?
        /// Pullar icin evet (3 tane Hayalet Pul alip desteye koyabilirsin),
        /// zar tilsimlari icin hayir (ikinci Ikiz Ruzgarlar hicbir sey yapmaz).
        /// Dukkan bu bayraga bakip urunu "Sahip olundu" diye kilitler.
        /// </summary>
        bool AllowsDuplicates { get; }
    }
}

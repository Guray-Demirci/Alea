namespace Tavla.Core.Abilities
{
    /// <summary>
    /// TUM pul yeteneklerinin ortak isaretleyicisi (marker interface).
    /// Neden gerekli? Cunku "Sisli Hane" dokumanda sunu soyluyor:
    /// "Bu haneye giren her ozel pulun TUM ozel yetenekleri devre disi kalir."
    /// Yetenekler ortak bir tipten turemezse boyle toplu bir kapatma yazilamaz;
    /// her yeni pul icin Sis koduna if eklemek gerekirdi. Bu marker sayesinde
    /// BaseChecker.GetAbility&lt;T&gt;() tek bir yerden hepsini suzebiliyor.
    /// </summary>
    public interface ICheckerAbility
    {
    }
}

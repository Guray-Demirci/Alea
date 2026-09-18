namespace Tavla.Core.Interfaces
{
    /// <summary>
    /// Oyuncunun oyuna BASLARKEN elinde hazir bulunan ogeleri tanimlar.
    /// IPurchasable'dan ayri tutulur: Bir oge satin alinabilir ama baslangicta verilmiyor olabilir
    /// (ya da tam tersi). Arayuz ayrimi (ISP) sayesinde siniflar sadece ihtiyaci olani uygular.
    /// </summary>
    public interface IStarterItem
    {
        /// <summary>Maca baslarken oyuncuya kac adet verilecegi.</summary>
        int StartingAmount { get; }

        /// <summary>Ucretsiz mi geliyor, yoksa once magazadan acilmasi mi gerekiyor?</summary>
        bool IsDefaultUnlocked { get; }
    }
}

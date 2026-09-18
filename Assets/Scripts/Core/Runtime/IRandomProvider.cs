namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Rastgelelik kaynagi soyutlamasi. Dokumandaki pullarin cogu %50/%25 gibi sansa dayaniyor.
    /// Unity.Engine tarafindaki Random sinifi KULLANILAMAZ (Core saf C#). Ayrica soyutlama sayesinde
    /// testlerde sabit tohumlu (deterministik) bir saglayici takip "Bar Pulu gercekten
    /// %50 kiriyor mu?" sorusunu guvenilir sekilde olcebiliriz.
    /// </summary>
    public interface IRandomProvider
    {
        /// <summary>[minInclusive, maxExclusive) araliginda tam sayi.</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>0..1 arasi olasilikla true doner. Orn: Chance(0.5f) -> %50.</summary>
        bool Chance(float probability);
    }
}

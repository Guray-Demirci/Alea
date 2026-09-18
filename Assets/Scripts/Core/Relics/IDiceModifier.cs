using System.Collections.Generic;
using Tavla.Core.Runtime;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Zar sistemine mudahale eden tilsimlar (dokumandaki A bolumu).
    /// IKI ayri hook var cunku dokumandaki 4 mekanik iki farkli ana dokunuyor:
    ///   ModifyFaces -> ATISTAN ONCE zarin yuzeylerini degistirir (Firtina Raporu)
    ///   ModifyRoll  -> ATISTAN SONRA sonucu yorumlar (Meridyen, Ikiz Ruzgarlar, Kartograf)
    /// </summary>
    public interface IDiceModifier
    {
        /// <summary>Kucuk deger once calisir. Yuzey degistirenler, sonuc yorumlayanlardan once gelmeli.</summary>
        int Priority { get; }

        /// <summary>Tilsim hala etkin mi? (Tek kullanimliklar tukenince false doner.)</summary>
        bool IsActive { get; }

        /// <summary>Zarin yuzeyindeki sayi havuzunu degistirir. Liste yerinde duzenlenir.</summary>
        void ModifyFaces(List<int> faces);

        /// <summary>Atilan sonucu degistirir.</summary>
        void ModifyRoll(DiceRoll roll, IRandomProvider random);
    }
}

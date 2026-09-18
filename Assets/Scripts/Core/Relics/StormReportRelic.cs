using System.Collections.Generic;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// A3 - Firtina Raporu — KALICI.
    /// "Zar havuzundaki istedigin bir rakami tamamen siler, yerine istedigin baska bir sayiyi kazir."
    /// Atistan ONCE calisir -> dusuk oncelik degeri.
    /// </summary>
    public class StormReportRelic : PermanentRelic
    {
        public override string ItemId => "relic_storm_report";
        public override string DisplayName => "Fırtına Raporu";
        public override int Price => 350;

        public override int Priority => 10;

        private readonly int _removedFace;
        private readonly int _engravedFace;

        /// <param name="removedFace">Silinecek yuz (orn. 1).</param>
        /// <param name="engravedFace">Yerine kazinacak sayi (orn. 6).</param>
        public StormReportRelic(int removedFace, int engravedFace)
        {
            _removedFace = removedFace;
            _engravedFace = engravedFace;
        }

        public override void ModifyFaces(List<int> faces)
        {
            // Tek bir yuzu degistiriyoruz; RemoveAll kullanirsak ust uste kazimalarda
            // ayni sayidan birden fazlasi varsa hepsi silinir - istemedigimiz sonuc.
            int index = faces.IndexOf(_removedFace);
            if (index < 0)
                return;

            faces[index] = _engravedFace;
        }
    }
}

using System.Collections.Generic;

namespace Tavla.Core.Relics
{
    /// <summary>
    /// Storm Report — PERMANENT.
    /// "Erases one number of your choice from the dice pool and engraves another number of your choice in its place."
    /// Runs BEFORE the roll -> low priority value.
    /// </summary>
    public class StormReportRelic : PermanentRelic
    {
        public override string ItemId => "relic_storm_report";
        public override string DisplayName => "Storm Report";
        public override int Price => 350;

        public override int Priority => 10;

        private readonly int _removedFace;
        private readonly int _engravedFace;

        /// <param name="removedFace">Face to erase (e.g. 1).</param>
        /// <param name="engravedFace">Number to engrave instead (e.g. 6).</param>
        public StormReportRelic(int removedFace, int engravedFace)
        {
            _removedFace = removedFace;
            _engravedFace = engravedFace;
        }

        public override void ModifyFaces(List<int> faces)
        {
            // Only one face is replaced; RemoveAll would erase every copy of the number when several
            // engravings stack on each other — not what we want.
            int index = faces.IndexOf(_removedFace);
            if (index < 0)
                return;

            faces[index] = _engravedFace;
        }
    }
}

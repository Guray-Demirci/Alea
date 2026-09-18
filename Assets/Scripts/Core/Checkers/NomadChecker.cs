using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Gocebe Pul. "Bastigi hanenin sagindaki ve solundaki haneler tamamen bossa,
    /// fazladan 3 hane daha ileri gitme hakki kazanir."
    /// Bonus ZAR DEGERINE eklenir, ekstra hamle hakki dogurmaz: 3 zariyla 6 hane gider.
    /// </summary>
    public class NomadChecker : BaseChecker, IExtraDistanceProvider, IPermanent
    {
        public const string Id = "checker_nomad";
        private const int BonusDistance = 3;

        public override string TypeId => Id;

        public int GetBonusDistance(GameState state, int fromIndex)
        {
            // Bardan giriste "komsu hane" kavrami yok -> bonus verilmez.
            if (!GameState.IsOnBoard(fromIndex))
                return 0;

            return IsSideEmpty(state, fromIndex - 1) && IsSideEmpty(state, fromIndex + 1)
                ? BonusDistance
                : 0;
        }

        /// <summary>Tahta kenari da "bos" sayilir — orada engelleyecek pul zaten olamaz.</summary>
        private static bool IsSideEmpty(GameState state, int index)
        {
            return !GameState.IsOnBoard(index) || state.Nodes[index].IsEmpty;
        }

        public string ItemId => Id;
        public string DisplayName => "Göçebe Pul";
        public int Price => 300;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public NomadChecker(int ownerId) : base(ownerId) { }
    }
}

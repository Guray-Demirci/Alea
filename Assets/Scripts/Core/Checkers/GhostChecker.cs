using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Hayalet Pul. "Rakibin kapilarinin uzerinden atlayabilir, blokajlari yok sayar."
    /// Parazit'ten FARKI: Hayalet blokaji yok sayarak haneye girer ve tek rakip pulu varsa
    /// onu normal sekilde kirar; sadece 2+ pullu kapida kirmadan yanlarina konar.
    /// </summary>
    public class GhostChecker : BaseChecker, IBlockadeBypass, IPermanent, IStarterItem
    {
        public const string Id = "checker_ghost";

        public override string TypeId => Id;

        // --- IBlockadeBypass ---
        public bool CanBypassBlockade(Node target) => true;

        // --- IPurchasable ---
        public string ItemId => Id;
        public string DisplayName => "Hayalet Pul";
        public int Price => 450;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        // --- IStarterItem ---
        public int StartingAmount => 1;
        public bool IsDefaultUnlocked => false;

        public GhostChecker(int ownerId) : base(ownerId) { }
    }
}

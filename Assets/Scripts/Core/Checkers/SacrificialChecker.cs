using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Fedakar Pul. "Rakip bu pulu kirdiginda pul bara gitmez, YOK OLUR.
    /// Ancak dukkandaki tum esyalarin fiyatini kalici olarak 2 altin ucuzlatir."
    /// Iki farkli sorumluluk, iki farkli arayuz:
    ///   ICustomCaptureDestination -> NEREYE gidecegi
    ///   ICaptureReactor           -> YAN ETKISI ne olacagi
    /// </summary>
    public class SacrificialChecker : BaseChecker, ICustomCaptureDestination, ICaptureReactor, IPermanent
    {
        public const string Id = "checker_sacrificial";
        private const int DiscountAmount = 2;

        public override string TypeId => Id;

        public CaptureDestination Destination => CaptureDestination.Destroyed;

        public void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex)
        {
            context.AddShopDiscount(DiscountAmount);
            context.Board.ReportAbility("SacrificeDiscount", this, nodeIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Fedakar Pul";
        public int Price => 150;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public SacrificialChecker(int ownerId) : base(ownerId) { }
    }
}

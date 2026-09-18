using Tavla.Core.Abilities;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Anka Pulu (The Phoenix). Iki katmanli bir puldur:
    ///   Tahta kurali : Kirildiginda BARA gitmez, kendi baslangic hanesine doner (esir dusmez).
    ///   Meta kurali  : Her kirilisinda "Kullerinden Dogus" tetiklenir -> sonraki dukkanda
    ///                  sadece Rare/Epic/Efsanevi urunler cikar ve %50 indirime girer.
    /// </summary>
    public class PhoenixChecker : BaseChecker, ICustomCaptureDestination, ICaptureReactor, IPermanent
    {
        public const string Id = "checker_phoenix";

        /// <summary>Kac kez kullerinden dogdugu — View'in tuy/alev efektini buyutmesi icin.</summary>
        public int RebirthCount { get; private set; }

        public override string TypeId => Id;

        public CaptureDestination Destination => CaptureDestination.HomeEntry;

        public void OnCaptured(IMatchContext context, int capturedByPlayerId, int nodeIndex)
        {
            RebirthCount++;
            context.TriggerLuxuryShop(OwnerId);
            context.Board.ReportAbility("PhoenixRebirth", this, nodeIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Anka Pulu";
        public int Price => 900;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public PhoenixChecker(int ownerId) : base(ownerId) { }
    }
}

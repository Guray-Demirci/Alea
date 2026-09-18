using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Kiyamet Pulu (The Doomsday). "Rakibin kapi aldigi bir haneye tam zariyla denk gelirse,
    /// o hanedeki TUM rakip pullarini tek seferde kirar ve hepsini bara sutlar."
    /// Klasik tavlanin en temel kisitini (kapiya girilemez) tersine ceviren pul.
    /// </summary>
    public class DoomsdayChecker : BaseChecker, IAreaCapture, IPermanent
    {
        public const string Id = "checker_doomsday";

        public override string TypeId => Id;

        /// <summary>Sadece gercek bir kapiya (2+ rakip pulu) karsi calisir; tek pulu normal kirar.</summary>
        public bool CanCaptureEntirePoint(Node target) => target.CountEnemiesOf(OwnerId) >= 2;

        public string ItemId => Id;
        public string DisplayName => "Kıyamet Pulu";
        public int Price => 1000;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public DoomsdayChecker(int ownerId) : base(ownerId) { }
    }
}

using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Kiyamet Pulu: "Rakibin kapi aldigi bir haneye tam zariyla denk gelirse,
    /// o hanedeki TUM rakip pullarini tek seferde kirar."
    /// Klasik tavlada kapiya girilemez; bu yetenek o kisiti tamamen tersine cevirir.
    /// </summary>
    public interface IAreaCapture : ICheckerAbility
    {
        /// <summary>Bu hedefteki tum rakip pullarini kirabilir mi?</summary>
        bool CanCaptureEntirePoint(Node target);
    }
}

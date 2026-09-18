using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// C2 - Sisli Hane (The Fog). "Bu haneye giren her ozel pulun TUM ozel yetenekleri,
    /// o hanede durdugu surece tamamen devre disi kalir (duz pula donusur)."
    ///
    /// MIMARININ SINAVI: Bu karo 18 pulun hicbirini tanimadan hepsini etkisizlestiriyor.
    /// Tek yaptigi BaseChecker'daki yetenek kapisini kapatmak; kural motorunun geri kalani
    /// zaten her seyi GetAbility uzerinden sordugu icin etki kendiliginden her yere yayiliyor.
    /// Yarin 50 pul daha eklensin — bu dosya degismez.
    /// </summary>
    public class FogTile : BaseTileEffect
    {
        public const string Id = "tile_fog";

        public override string EffectId => Id;

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            checker.SetAbilitiesSuppressed(true);
            context.Board.ReportAbility("FogEnter", checker, node.Index);
        }

        public override void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker)
        {
            checker.SetAbilitiesSuppressed(false);
            context.Board.ReportAbility("FogExit", checker, node.Index);
        }
    }
}

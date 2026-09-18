using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// C6 - Tahta Kazik Hanesi (The Spike Pit). "Bu haneye basan her pul aninda kirilir ve bara sutlanir.
    /// Buradan sadece Zirhli Pul gibi ozel pullar hasar almadan gecebilir — kazigi kirar
    /// ve haneyi normal karoya donusturur."
    /// Somut sinifi degil YETENEGI sorguluyoruz: ileride bagisiklik kazanan her pul otomatik olarak gecebilir.
    /// </summary>
    public class SpikePitTile : BaseTileEffect
    {
        public const string Id = "tile_spikepit";

        private bool _broken;

        public override string EffectId => Id;

        /// <summary>Kazik kirildiysa karo tukenmistir; GameState onu haneden temizler.</summary>
        public override bool IsExpired => _broken;

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            ICaptureImmunity immunity = checker.GetAbility<ICaptureImmunity>();

            if (immunity != null && immunity.IsImmune)
            {
                _broken = true;
                context.Board.ReportAbility("SpikeBroken", checker, node.Index);
                return;
            }

            context.Board.ReportAbility("SpikeHit", checker, node.Index);
            context.Board.ForceCapture(checker, node.Index, OpponentOf(checker));
        }
    }
}

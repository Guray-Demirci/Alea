using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// C5 - Zindan Hanesi (Iron Cage). Acil-kapan dongusu:
    ///   Kapali tur: icerideki pullar oynatilamaz, disaridan pul giremez, kapi alinamaz.
    ///   Acik tur  : hane tamamen normal davranir.
    /// Uc ayri izin sorgusunu da ezen tek karo bu — hook setinin neden bu kadar
    /// ayrintili oldugunun cevabi burada.
    /// </summary>
    public class DungeonTile : BaseTileEffect
    {
        public const string Id = "tile_dungeon";

        public override string EffectId => Id;

        /// <summary>Parmakliklar kapali mi? View bu degere bakip kafes animasyonunu oynatir.</summary>
        public bool IsClosed { get; private set; }

        public DungeonTile(bool startClosed = true)
        {
            IsClosed = startClosed;
        }

        public override bool CanLandOn(BaseChecker checker) => !IsClosed;

        public override bool CanLeaveFrom(BaseChecker checker) => !IsClosed;

        public override bool AllowsBlockade => !IsClosed;

        public override void OnTurnPassed(IMatchContext context, Node node)
        {
            IsClosed = !IsClosed;
            context.Board.ReportAbility(IsClosed ? "DungeonClosed" : "DungeonOpened", null, node.Index);
        }
    }
}

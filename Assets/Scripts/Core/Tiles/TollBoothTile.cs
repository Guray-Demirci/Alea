using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// C4 - Haydut Kampi (The Toll Booth). "Bu haneden GECEN veya buraya OTURAN her pul icin
    /// kasandan -2 Altin eksilir. Paran yoksa altinin eksiye dusmez ama skordan puan silinir."
    /// Borc kurali MatchContext.AddGold icinde tek yerde uygulanir — karonun bilmesi gerekmez.
    /// </summary>
    public class TollBoothTile : BaseTileEffect
    {
        public const string Id = "tile_tollbooth";
        private const int TollCost = -2;

        public override string EffectId => Id;

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            Collect(context, node, checker);
        }

        public override void OnCheckerPassedOver(IMatchContext context, Node node, BaseChecker checker)
        {
            Collect(context, node, checker);
        }

        private static void Collect(IMatchContext context, Node node, BaseChecker checker)
        {
            context.AddGold(checker.OwnerId, TollCost);
            context.Board.ReportAbility("TollPaid", checker, node.Index);
        }
    }
}

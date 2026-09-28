using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Toll Booth Tile. "For every checker that PASSES this point or SITS on it, 2 gold is taken from
    /// your purse. If you have no money your gold does not go negative, but score points are deducted."
    /// The debt rule is applied in one place, <see cref="MatchContext.AddGold"/> — the tile does not need to know it.
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
            context.Board.ReportAbility(AbilityIds.TollPaid, checker, node.Index);
        }
    }
}

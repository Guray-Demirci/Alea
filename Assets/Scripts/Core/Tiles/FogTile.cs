using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Fog Tile. "ALL special abilities of every special checker that enters this point are
    /// completely disabled for as long as it stays there (it becomes a plain checker)."
    ///
    /// THE ARCHITECTURE'S TEST: this tile neutralizes all 18 checkers without knowing any of them.
    /// All it does is close the ability gate in BaseChecker; the rest of the rules engine asks
    /// everything through GetAbility, so the effect spreads everywhere on its own.
    /// Add 50 more checkers tomorrow — this file does not change.
    /// </summary>
    public class FogTile : BaseTileEffect
    {
        public const string Id = "tile_fog";

        public override string EffectId => Id;

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            checker.SetAbilitiesSuppressed(true);
            context.Board.ReportAbility(AbilityIds.FogEnter, checker, node.Index);
        }

        public override void OnCheckerLeft(IMatchContext context, Node node, BaseChecker checker)
        {
            checker.SetAbilitiesSuppressed(false);
            context.Board.ReportAbility(AbilityIds.FogExit, checker, node.Index);
        }
    }
}

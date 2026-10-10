using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Dungeon Tile (Iron Cage). Open/close cycle:
    ///   Closed round: checkers inside cannot move, no checker can enter, no point can be made.
    ///   Open round  : the point behaves completely normally.
    /// The cycle runs per ROUND, not per turn: flipping on every turn would show one player the cage
    /// always closed and the other always open. Per round, each player sees it closed on one turn
    /// and open on the next.
    /// The only tile that overrides all three permission queries — it is the reason the hook set
    /// is so fine-grained.
    /// </summary>
    public class DungeonTile : BaseTileEffect
    {
        public const string Id = "tile_dungeon";

        public override string EffectId => Id;

        /// <summary>Are the bars closed? The View uses this to play the cage animation.</summary>
        public bool IsClosed { get; private set; }

        public DungeonTile(bool startClosed = true)
        {
            IsClosed = startClosed;
        }

        public override bool CanLandOn(BaseChecker checker) => !IsClosed;

        public override bool CanLeaveFrom(BaseChecker checker) => !IsClosed;

        public override bool AllowsBlockade => !IsClosed;

        public override void OnRoundPassed(IMatchContext context, Node node)
        {
            IsClosed = !IsClosed;
            context.Board.ReportAbility(IsClosed ? AbilityIds.DungeonClosed : AbilityIds.DungeonOpened,
                                        null, node.Index);
        }
    }
}

using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Maelstrom Tile. "A checker that lands here is caught by the current and flung to a random point.
    /// If the opponent holds that point, the checker is captured and sent to the bar."
    /// </summary>
    public class MaelstromTile : BaseTileEffect
    {
        public const string Id = "tile_maelstrom";

        /// <summary>
        /// Re-entrancy guard. Two maelstroms flinging checkers at each other would loop forever;
        /// needed because TeleportChecker also fires OnCheckerLanded of the destination tile.
        /// </summary>
        private bool _resolving;

        public override string EffectId => Id;

        public override void OnCheckerLanded(IMatchContext context, Node node, BaseChecker checker)
        {
            if (_resolving)
                return;

            _resolving = true;
            try
            {
                int destination = PickDestination(context, node.Index);
                context.Board.ReportAbility(AbilityIds.MaelstromPull, checker, node.Index);
                context.Board.TeleportChecker(checker, node.Index, destination);

                // If the opponent holds the point it was flung to, the checker is captured.
                Node landed = context.Board.Nodes[destination];
                if (landed.IsBlockedFor(checker.OwnerId))
                    context.Board.ForceCapture(checker, destination, OpponentOf(checker));
            }
            finally
            {
                _resolving = false;
            }
        }

        /// <summary>Picks a random point other than the maelstrom's own point.</summary>
        private static int PickDestination(IMatchContext context, int currentIndex)
        {
            int destination = context.Random.Range(0, GameState.NodeCount - 1);

            // Skip its own point: picking from [0, N-1) and shifting avoids re-rolling.
            if (destination >= currentIndex)
                destination++;

            return destination;
        }
    }
}

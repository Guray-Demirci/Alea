using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// Plunder Cove Tile. "Resets ALL score points produced this turn by the player whose checker stands here."
    /// Runs at the end of the turn, because the score to be reset must have been produced first.
    /// The only tile that acts on its checkers outside GameState's per-checker hooks, so it checks
    /// <see cref="Checkers.BaseChecker.IsAffectedByTiles"/> itself.
    /// </summary>
    public class PlunderCoveTile : BaseTileEffect
    {
        public const string Id = "tile_plundercove";

        public override string EffectId => Id;

        public override void OnTurnPassed(IMatchContext context, Node node, int endingPlayerId)
        {
            if (node.IsEmpty)
                return;

            // Mark owners as we go so the same player is not reset twice.
            bool[] plundered = new bool[GameState.PlayerCount];

            for (int i = 0; i < node.CheckerCount; i++)
            {
                // A checker immune to tiles (Reckless) does not expose its owner's score.
                if (!node.Checkers[i].IsAffectedByTiles)
                    continue;

                int ownerId = node.Checkers[i].OwnerId;
                if (plundered[ownerId])
                    continue;

                plundered[ownerId] = true;
                context.ResetTurnScore(ownerId);
                context.Board.ReportAbility(AbilityIds.Plundered, node.Checkers[i], node.Index);
            }
        }
    }
}

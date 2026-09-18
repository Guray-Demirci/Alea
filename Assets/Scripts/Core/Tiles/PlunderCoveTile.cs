using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// C8 - Korsan Koyu / Talan Hanesi. "Bu hanede duran pulun, o tur urettigi TUM skor puanlarini sifirlar."
    /// Tur sonunda calisir, cunku sifirlanacak puanin once uretilmis olmasi gerekir.
    /// </summary>
    public class PlunderCoveTile : BaseTileEffect
    {
        public const string Id = "tile_plundercove";

        public override string EffectId => Id;

        public override void OnTurnPassed(IMatchContext context, Node node)
        {
            if (node.IsEmpty)
                return;

            // Ayni oyuncuyu iki kez sifirlamamak icin sahipleri isaretleyerek geziyoruz.
            bool[] plundered = new bool[GameState.PlayerCount];

            for (int i = 0; i < node.StoneCount; i++)
            {
                int ownerId = node.Checkers[i].OwnerId;
                if (plundered[ownerId])
                    continue;

                plundered[ownerId] = true;
                context.ResetTurnScore(ownerId);
                context.Board.ReportAbility("Plundered", node.Checkers[i], node.Index);
            }
        }
    }
}

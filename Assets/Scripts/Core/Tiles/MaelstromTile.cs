using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Tiles
{
    /// <summary>
    /// C3 - Girdap Hanesi (The Maelstrom). "Bu haneye basan pul, akintiya kapilarak rastgele
    /// bir haneye firlatilir. Firlatildigi hanede rakibin kapisi varsa pul direkt kirilir ve bara gider."
    /// </summary>
    public class MaelstromTile : BaseTileEffect
    {
        public const string Id = "tile_maelstrom";

        /// <summary>
        /// Yeniden giris kilidi. Iki girdap birbirine pul firlatirsa sonsuz dongu olusur;
        /// TeleportChecker hedefteki karonun OnCheckerLanded'ini da tetikledigi icin bu sart.
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
                context.Board.ReportAbility("MaelstromPull", checker, node.Index);
                context.Board.TeleportChecker(checker, node.Index, destination);

                // Firlatildigi yerde rakip kapisi varsa pul kirilir.
                Node landed = context.Board.Nodes[destination];
                if (landed.IsBlockedFor(checker.OwnerId))
                    context.Board.ForceCapture(checker, destination, OpponentOf(checker));
            }
            finally
            {
                _resolving = false;
            }
        }

        /// <summary>Girdabin kendi hanesi haric rastgele bir hane secer.</summary>
        private static int PickDestination(IMatchContext context, int currentIndex)
        {
            int destination = context.Random.Range(0, GameState.NodeCount - 1);

            // Kendi hanesini atla: [0, N-1) araligindan secip kaydirarak tekrar denemeye gerek kalmiyor.
            if (destination >= currentIndex)
                destination++;

            return destination;
        }
    }
}

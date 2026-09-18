using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Acgozlu Pul. "Oyunda tek basina (acik olarak) kaldigi HER TUR icin ekstra altin uretir.
    /// Risk: Rakip her an kirabilir."
    /// Kendi konumunu bilmez; tur sonunda GameState hangi hanede oldugunu soyler.
    /// </summary>
    public class GreedyChecker : BaseChecker, ITurnEndReactor, IPermanent
    {
        public const string Id = "checker_greedy";
        private const int GoldPerTurn = 3;

        public override string TypeId => Id;

        public void OnTurnEnded(IMatchContext context, int nodeIndex)
        {
            Node node = context.Board.Nodes[nodeIndex];

            // "Acik" olmak = hanede yapayalniz durmak. Yanina biri gelirse uretim durur.
            if (node.StoneCount != 1)
                return;

            context.AddGold(OwnerId, GoldPerTurn);
            context.Board.ReportAbility("GreedyIncome", this, nodeIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Açgözlü Pul";
        public int Price => 260;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public GreedyChecker(int ownerId) : base(ownerId) { }
    }
}

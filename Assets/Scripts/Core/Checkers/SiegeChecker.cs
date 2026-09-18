using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Kusatma Pulu. "Bu pulun bulundugu haneye kendi renginden baska bir pul gelip
    /// KAPI aldiginda, o el yaptigin tum hamlelerin carpanini KALICI olarak X2 yapar."
    /// isMover=false hook'unun varlik sebebi bu puldur: tetikleyici kendi hamlesi degil,
    /// bir dostunun hamlesidir.
    /// </summary>
    public class SiegeChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_siege";
        private const float SiegeMultiplier = 2f;

        /// <summary>Kalici carpan bir kez verilir; yoksa her hamlede katlanarak patlar.</summary>
        private bool _triggered;

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            // Kendi hamlem degil, dostumun hamlesi olmali.
            if (isMover || _triggered || result.PlayerId != OwnerId)
                return;

            // Kapi olustu mu? (kendim + gelen dost pul = en az 2)
            Node node = context.Board.Nodes[result.ToIndex];
            if (node.CountFor(OwnerId) < 2)
                return;

            _triggered = true;
            context.AddScoreMultiplier(OwnerId, SiegeMultiplier, true);
            context.Board.ReportAbility("SiegeLocked", this, result.ToIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Kuşatma Pulu";
        public int Price => 650;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public SiegeChecker(int ownerId) : base(ownerId) { }
    }
}

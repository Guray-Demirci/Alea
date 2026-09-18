using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Ikiz Pul (Gemini). "Dukkandan her zaman CIFT olarak satilir. Iki pulu yan yana
    /// iki haneye getirmeyi basarirsan fazladan puan ve carpan kazanirsin."
    /// PairId ile esini tanir; CreatePair() disinda tek basina uretilmemelidir.
    /// </summary>
    public class GeminiChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_gemini";

        private const int AdjacencyScore = 200;
        private const float AdjacencyMultiplier = 1.5f;

        /// <summary>Ayni PairId'ye sahip iki pul birbirinin ikizidir.</summary>
        public int PairId { get; private set; }

        public override string TypeId => Id;

        private GeminiChecker(int ownerId, int pairId) : base(ownerId)
        {
            PairId = pairId;
        }

        /// <summary>Dukkan bu pulu tek satamaz — uretim her zaman cifttir.</summary>
        public static GeminiChecker[] CreatePair(int ownerId, int pairId)
        {
            return new[]
            {
                new GeminiChecker(ownerId, pairId),
                new GeminiChecker(ownerId, pairId)
            };
        }

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            // Sadece hareket eden ikiz kontrol etsin ki bonus iki kez yazilmasin.
            if (!isMover)
                return;

            int myIndex = result.ToIndex;
            if (!IsTwinAt(context, myIndex - 1) && !IsTwinAt(context, myIndex + 1))
                return;

            context.AddScore(OwnerId, AdjacencyScore);
            context.AddScoreMultiplier(OwnerId, AdjacencyMultiplier, false);
            context.Board.ReportAbility("GeminiLink", this, myIndex);
        }

        private bool IsTwinAt(IMatchContext context, int index)
        {
            if (!GameState.IsOnBoard(index))
                return false;

            Node node = context.Board.Nodes[index];
            for (int i = 0; i < node.StoneCount; i++)
            {
                GeminiChecker twin = node.Checkers[i] as GeminiChecker;
                if (twin != null && !ReferenceEquals(twin, this) && twin.PairId == PairId)
                    return true;
            }
            return false;
        }

        public string ItemId => Id;
        public string DisplayName => "İkiz Pul (Çift)";
        public int Price => 700;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;
    }
}

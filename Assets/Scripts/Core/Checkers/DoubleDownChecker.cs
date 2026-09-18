using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// B - Gele Pulu (Double Down). "%25 ihtimalle o el attigin zarlardan birini cift saydirir
    /// (ekstra hamle hakki), %50 ihtimalle o eldeki kalan tum hamle haklarini kaybedersin."
    /// Klasik "yuksek risk / yuksek kazanc" pulu.
    /// </summary>
    public class DoubleDownChecker : BaseChecker, IMoveReactor, IPermanent
    {
        public const string Id = "checker_doubledown";

        private const int BonusThreshold = 25;   // %25 kazanc
        private const int PenaltyThreshold = 75; // sonraki %50 ceza

        public override string TypeId => Id;

        public void OnMoveCompleted(IMatchContext context, MoveResult result, bool isMover)
        {
            if (!isMover || context.CurrentRoll == null)
                return;

            int roll = context.Random.Range(0, 100);

            if (roll < BonusThreshold)
            {
                // Ekstra hamle: son kullanilan zar degeri kadar.
                int bonusValue = context.CurrentRoll.First;
                context.CurrentRoll.GrantExtraMove(bonusValue);
                context.Board.ReportAbility("DoubleDownBonus", this, result.ToIndex);
            }
            else if (roll < PenaltyThreshold)
            {
                context.CurrentRoll.LoseRemainingMoves();
                context.Board.ReportAbility("DoubleDownPenalty", this, result.ToIndex);
            }
        }

        public string ItemId => Id;
        public string DisplayName => "Gele Pulu";
        public int Price => 380;

        /// <summary>Ayni puldan birden fazlasi desteye konabilir.</summary>
        public bool AllowsDuplicates => true;

        public DoubleDownChecker(int ownerId) : base(ownerId) { }
    }
}
